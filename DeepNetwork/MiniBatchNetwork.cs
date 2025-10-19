using DeepNetwork.ActivationFunctions;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using SystemVector = System.Numerics.Vector;
using SystemVectorT = System.Numerics.Vector<double>;

namespace DeepNetwork;

public class MiniBatchMatrixNetwork : IStandardNetwork
{
    private const double LearningRate = 0.001;
    private const double NearNullValue = 1e-11d;

    private readonly IActivationFunction[] _activations;

    // Adam optimizer state
    private readonly Matrix<double>[] _adamM;
    private readonly Vector<double>[] _adamMBias;
    private readonly Matrix<double>[] _adamV;
    private readonly Vector<double>[] _adamVBias;

    private readonly ConcurrentDictionary<string, double[]> _cachedValues = new();
    private readonly ConcurrentQueue<string> _cacheKeys = new();
    private readonly Random _rand = new();
    private readonly int[] _structure;
    private int _adamT;

    // Matrix/Vector representations for batch training
    private Vector<double>[] _biases = [];
    private int[] _biasOffsets = [];
    private double[] _flatBiases = [];

    // Flat arrays for ultra-fast foward pass
    private double[] _flatWeights = [];
    private int[] _layerOffsets = [];
    private double[] _values = [];
    private int[] _weightOffsets = [];

    private Matrix<double>[] _weights;
    public static string NetworkName { get; } = nameof(MiniBatchMatrixNetwork);
    public double Error { get; set; }
    public double ExplorationFactor { get; set; }
    public Vector<double>[] GradientBiases { get; private set; }
    public Matrix<double>[] Gradients { get; private set; }
    public double LastError { get; set; }
    public bool Softmax { get; }
    public bool Trained { get; set; }

    static MiniBatchMatrixNetwork()
    {
        // needed to use performant matrix operations
        Control.UseNativeMKL();
    }

    public MiniBatchMatrixNetwork(int[] structure, bool isSoftmax)
    {
        _structure = structure;
        Softmax = isSoftmax;
        int layers = structure.Length - 1;

        // Initialize flat arrays for fast inference
        InitializeFlatArrays();

        // Initialize matrix/vector representations for batch training
        _weights = new Matrix<double>[layers];
        _biases = new Vector<double>[layers];
        _activations = new IActivationFunction[structure.Length];
        _adamM = new Matrix<double>[layers];
        _adamV = new Matrix<double>[layers];
        _adamMBias = new Vector<double>[layers];
        _adamVBias = new Vector<double>[layers];
        Gradients = new Matrix<double>[layers];
        GradientBiases = new Vector<double>[layers];
        _adamT = 0;

        for (int i = 0; i < layers; i++)
        {
            _weights[i] = Matrix<double>.Build.Dense(structure[i + 1], structure[i]);
            _biases[i] = Vector<double>.Build.Dense(structure[i + 1]);
            _adamM[i] = Matrix<double>.Build.Dense(structure[i + 1], structure[i]);
            _adamV[i] = Matrix<double>.Build.Dense(structure[i + 1], structure[i]);
            _adamMBias[i] = Vector<double>.Build.Dense(structure[i + 1]);
            _adamVBias[i] = Vector<double>.Build.Dense(structure[i + 1]);
            Gradients[i] = Matrix<double>.Build.Dense(structure[i + 1], structure[i]);
            GradientBiases[i] = Vector<double>.Build.Dense(structure[i + 1]);

            InitWeights(_weights[i], structure[i], structure[i + 1], _rand);
            InitBiases(_biases[i], _rand);
        }

        // Sync matrices to flat arrays
        SyncMatricesToFlat();

        for (int i = 0; i < structure.Length; i++)
        {
            _activations[i] = i == structure.Length - 1 && Softmax
                //? new SigmoidActivationFunction()
                ? new SoftMaxActivationFunction()
                //: new TanhActivationFunction();
                : new LeakyReLUActivationFunction();
        }
    }

    public static IStandardNetwork? CreateFromFile(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return null;
        }

        string json = File.ReadAllText(fileName);
        var jObject = JObject.Parse(json);

        //Check if the file is for this network type
        string name = jObject[nameof(NetworkName)]?.ToObject<string>()!;
        if (name?.Equals(NetworkName) != true)
        {
            return null;
        }

        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new DenseMatrixConverter());
        settings.Converters.Add(new DenseVectorConverter());
        int[] structure = jObject[nameof(_structure)]!.ToObject<int[]>()!;
        bool isSoftmax = jObject[nameof(Softmax)]?.ToObject<bool>() ?? false;

        var network = new MiniBatchMatrixNetwork(structure, isSoftmax)
        {
            _weights = jObject[nameof(_weights)]!.ToObject<DenseMatrix[]>(JsonSerializer.Create(settings))!,
            _biases = jObject[nameof(_biases)]!.ToObject<DenseVector[]>(JsonSerializer.Create(settings))!,
            Trained = true
        };

        network.SyncMatricesToFlat();

        // Restore activation functions
        string[] activationFunctionTypes = jObject["ActivationFunctionTypes"]!.ToObject<string[]>()!;
        for (int i = 0; i < activationFunctionTypes.Length; i++)
        {
            network._activations[i] = activationFunctionTypes[i] switch
            {
                nameof(SigmoidActivationFunction) => new SigmoidActivationFunction(),
                nameof(LeakyReLUActivationFunction) => new LeakyReLUActivationFunction(),
                nameof(TanhActivationFunction) => new TanhActivationFunction(),
                nameof(SoftMaxActivationFunction) => new SoftMaxActivationFunction(),
                _ => throw new InvalidOperationException($"Unknown activation function: {activationFunctionTypes[i]}")
            };
        }

        return network;
    }

    // Ultra-fast single inference using flat arrays and SIMD
    public double[] Calculate(double[] input)
    {
        Buffer.BlockCopy(input, 0, _values, 0, input.Length * sizeof(double));

        int layerStart = 0;
        int weightIndex = 0;
        int biasIndex = 0;

        for (int layer = 1; layer < _structure.Length; layer++)
        {
            int prevLayerSize = _structure[layer - 1];
            int currLayerSize = _structure[layer];
            int currLayerStart = layerStart + prevLayerSize;

            Span<double> prevValues = _values.AsSpan(layerStart, prevLayerSize);
            Span<double> currValues = _values.AsSpan(currLayerStart, currLayerSize);

            // Compute weighted sums for the current layer
            for (int node = 0; node < currLayerSize; node++)
            {
                Span<double> weights = _flatWeights.AsSpan(weightIndex, prevLayerSize);
                double weightedSum = DotProduct(prevValues, weights) + _flatBiases[biasIndex];
                currValues[node] = weightedSum;
                weightIndex += prevLayerSize;
                biasIndex++;
            }

            // Do activation on whole span
            _activations[layer].Calculate(currValues);

            layerStart = currLayerStart;
        }

        int lastLayerStart = _values.Length - _structure[^1];
        return _values.AsSpan(lastLayerStart, _structure[^1]).ToArray();
    }

    public void ClearCache()
    {
        _cachedValues.Clear();
        _cacheKeys.Clear();
    }

    public IStandardNetwork Clone()
    {
        var clone = new MiniBatchMatrixNetwork(_structure, Softmax);
        for (int i = 0; i < _weights.Length; i++)
        {
            clone._weights[i] = _weights[i].Clone();
            clone._biases[i] = _biases[i].Clone();
        }

        clone.SyncMatricesToFlat();
        clone.Trained = Trained;
        clone.Error = Error;
        clone.LastError = LastError;

        return clone;
    }

    public void SaveToFile(string fileName)
    {
        SyncFlatToMatrices();

        // Serialize activation function types as strings
        string[] activationFunctionTypes = [.. _activations.Select(f => f.GetType().Name)];

        var wrapper = new
        {
            Softmax,
            NetworkName,
            _weights,
            _structure,
            _biases,
            ActivationFunctionTypes = activationFunctionTypes,
        };

        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new DenseMatrixConverter());
        settings.Converters.Add(new DenseVectorConverter());

        string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented, settings);
        File.WriteAllText(fileName, json);
    }

    public double TrainMiniBatch(double[][] inputs, double[][] targets, int batchSize, double learningRate = LearningRate)
    {
        for (int i = 0; i < Gradients.Length; i++)
        {
            Gradients[i].Clear();
            GradientBiases[i].Clear();
        }

        int total = inputs.Length;
        int[] randomBatchIndex = [.. Enumerable.Range(0, total).OrderBy(_ => _rand.Next())];
        int totalCount = total * _structure[^1];
        int numBatches = (total + batchSize - 1) / batchSize;

        // Create copies for threads to use
        var weightsSnapshot = new Matrix<double>[_weights.Length];
        var biasesSnapshot = new Vector<double>[_biases.Length];
        for (int i = 0; i < _weights.Length; i++)
        {
            weightsSnapshot[i] = _weights[i].Clone();
            biasesSnapshot[i] = _biases[i].Clone();
        }

        var batchResults = new (Matrix<double>[] gradients, Vector<double>[] biasGradients, double errorSum, int sampleCount)[numBatches];

        Parallel.For(0, numBatches, batchIndex =>
        {
            int batchStart = batchIndex * batchSize;
            int actualBatchSize = Math.Min(batchSize, total - batchStart);

            Matrix<double> batchInputs = Matrix<double>.Build.Dense(actualBatchSize, _structure[0]);
            Matrix<double> batchTargets = Matrix<double>.Build.Dense(actualBatchSize, _structure[^1]);

            // Fill batch data
            for (int i = 0; i < actualBatchSize; i++)
            {
                int index = randomBatchIndex[batchStart + i];
                for (int j = 0; j < _structure[0]; j++)
                {
                    batchInputs[i, j] = inputs[index][j];
                }

                for (int j = 0; j < _structure[^1]; j++)
                {
                    batchTargets[i, j] = targets[index][j];
                }
            }

            (Matrix<double>[] batchGradients, Vector<double>[] batchBiasGradients, Matrix<double> output) =
                ComputeBatchGradients(batchInputs, batchTargets, weightsSnapshot, biasesSnapshot, _activations);

            // Calculate error for this batch
            double batchErrorSum = 0;
            bool isSoftmaxOutput = _activations[^1] is SoftMaxActivationFunction;

            for (int i = 0; i < actualBatchSize; i++)
            {
                for (int j = 0; j < _structure[^1]; j++)
                {
                    double target = batchTargets[i, j];
                    double outputValue = output[i, j];

                    if (isSoftmaxOutput)
                    {
                        // Cross-entropy loss for softmax outputs
                        double p = Math.Max(target, NearNullValue);
                        double q = Math.Max(outputValue, NearNullValue);
                        batchErrorSum += p * Math.Log(p / q);
                    }
                    else
                    {
                        // Mean Squared Error
                        double diff = outputValue - target;
                        batchErrorSum += 0.5 * diff * diff;
                    }
                }
            }

            batchResults[batchIndex] = (batchGradients, batchBiasGradients, batchErrorSum, actualBatchSize);
        });

        // Merge results from batches
        double errorSum = 0;
        int totalSamplesAccumulated = 0;

        for (int batchIndex = 0; batchIndex < numBatches; batchIndex++)
        {
            var (batchGradients, batchBiasGradients, batchErrorSum, sampleCount) = batchResults[batchIndex];

            // Weight the gradients by batch size
            for (int l = 0; l < Gradients.Length; l++)
            {
                Gradients[l] += batchGradients[l] * sampleCount;
                GradientBiases[l] += batchBiasGradients[l] * sampleCount;
            }

            errorSum += batchErrorSum;
            totalSamplesAccumulated += sampleCount;
        }

        // Average gradients
        for (int l = 0; l < Gradients.Length; l++)
        {
            Gradients[l] /= totalSamplesAccumulated;
            GradientBiases[l] /= totalSamplesAccumulated;
        }

        UpdateWeightsAdam(learningRate);
        SyncMatricesToFlat();

        double safeTotal = Math.Max(totalCount, NearNullValue);
        Error = errorSum / safeTotal;
        LastError = Error;

        return Error;
    }

    private static void ApplyActivation(Matrix<double> values, IActivationFunction activationFunction)
    {
        for (int i = 0; i < values.RowCount; i++)
        {
            double[] tempRow = new double[values.ColumnCount];
            for (int j = 0; j < values.ColumnCount; j++)
            {
                tempRow[j] = values[i, j];
            }

            activationFunction.Calculate(tempRow);
            for (int j = 0; j < values.ColumnCount; j++)
            {
                values[i, j] = tempRow[j];
            }
        }
    }

    private static (Matrix<double>[] gradients, Vector<double>[] biasGradients, Matrix<double> output)
        ComputeBatchGradients(
            Matrix<double> batchInputs,
            Matrix<double> batchTargets,
            Matrix<double>[] weights,
            Vector<double>[] biases,
            IActivationFunction[] activationFunctions)
    {
        int layers = weights.Length;
        var values = new Matrix<double>[layers + 1];
        var tempValues = new Matrix<double>[layers];
        values[0] = batchInputs;

        // Forwards
        for (int l = 0; l < layers; l++)
        {
            tempValues[l] = values[l] * weights[l].Transpose();
            tempValues[l] = tempValues[l] + VectorToRowMatrix(biases[l], tempValues[l].RowCount);
            values[l + 1] = tempValues[l].Clone();
            ApplyActivation(values[l + 1], activationFunctions[l + 1]);
        }

        Matrix<double> output = values[^1].Clone();

        // Backwards
        var deltas = new Matrix<double>[layers];
        bool softmaxLast = activationFunctions[^1] is SoftMaxActivationFunction;
        if (softmaxLast)
        {
            // only usable if last layer is soft max in combination with cross entropy loss function
            deltas[^1] = output - batchTargets;
        }
        else
        {
            // when output layer is sigmoid or tanh
            deltas[^1] = Matrix<double>.Build.Dense(output.RowCount, output.ColumnCount);
            for (int i = 0; i < output.RowCount; i++)
            {
                for (int j = 0; j < output.ColumnCount; j++)
                {
                    double target = batchTargets[i, j];
                    double outputValue = output[i, j];
                    double difference = (outputValue - target);
                    deltas[^1][i, j] = difference * activationFunctions[^1].Derivative(output[i, j]);
                }
            }
        }

        for (int l = layers - 2; l >= 0; l--)
        {
            deltas[l] = deltas[l + 1] * weights[l + 1];
            MultiplyByDerivative(deltas[l], values[l + 1], activationFunctions[l + 1]);
        }

        var gradients = new Matrix<double>[layers];
        var biasGradients = new Vector<double>[layers];
        for (int l = 0; l < layers; l++)
        {
            gradients[l] = values[l].TransposeThisAndMultiply(deltas[l]).Transpose() / batchInputs.RowCount;
            biasGradients[l] = deltas[l].ColumnSums() / batchInputs.RowCount;
        }

        return (gradients, biasGradients, output);
    }

    private static double DotProduct(ReadOnlySpan<double> values, ReadOnlySpan<double> weights)
    {
        int i = 0;
        double sum = 0.0;

        if (SystemVector.IsHardwareAccelerated)
        {
            int simdLength = SystemVectorT.Count;
            SystemVectorT vsum = SystemVectorT.Zero;
            for (; i <= values.Length - simdLength; i += simdLength)
            {
                var va = new SystemVectorT(values.Slice(i, simdLength));
                var vb = new SystemVectorT(weights.Slice(i, simdLength));
                vsum += va * vb;
            }

            for (int j = 0; j < simdLength; j++)
            {
                sum += vsum[j];
            }
        }

        for (; i < values.Length; i++)
        {
            sum += values[i] * weights[i];
        }

        return sum;
    }

    private static void InitBiases(Vector<double> biases, Random random)
    {
        for (int i = 0; i < biases.Count; i++)
        {
            biases[i] = random.NextDouble() - 0.5;
        }
    }

    private static void InitWeights(Matrix<double> weights, int inputSize, int outputSize, Random random)
    {
        double beta = 0.7 * Math.Pow(outputSize, 1.0 / inputSize);
        for (int i = 0; i < outputSize; i++)
        {
            double norm = 0.0;
            for (int j = 0; j < inputSize; j++)
            {
                weights[i, j] = random.NextDouble() - 0.5;
                norm += weights[i, j] * weights[i, j];
            }

            norm = Math.Sqrt(norm);
            for (int j = 0; j < inputSize; j++)
            {
                weights[i, j] = norm > 0 ? beta * weights[i, j] / norm : 0.0;
            }
        }
    }

    private static void MultiplyByDerivative(Matrix<double> delta, Matrix<double> values, IActivationFunction act)
    {
        for (int i = 0; i < delta.RowCount; i++)
        {
            for (int j = 0; j < delta.ColumnCount; j++)
            {
                delta[i, j] *= act.Derivative(values[i, j]);
            }
        }
    }

    private static Matrix<double> VectorToRowMatrix(Vector<double> v, int rows)
    {
        Matrix<double> matrix = Matrix<double>.Build.Dense(rows, v.Count);
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < v.Count; j++)
            {
                matrix[i, j] = v[j];
            }
        }

        return matrix;
    }

    private void InitializeFlatArrays()
    {
        // Calculate total sizes
        int totalNeuronCount = _structure.Sum();
        _values = new double[totalNeuronCount];

        int totalWeightCount = 0;
        int totalBiasCount = 0;
        for (int i = 0; i < _structure.Length - 1; i++)
        {
            totalWeightCount += _structure[i] * _structure[i + 1];
            totalBiasCount += _structure[i + 1];
        }

        _flatWeights = new double[totalWeightCount];
        _flatBiases = new double[totalBiasCount];

        // Calculate offsets
        _layerOffsets = new int[_structure.Length];
        _weightOffsets = new int[_structure.Length - 1];
        _biasOffsets = new int[_structure.Length - 1];

        _layerOffsets[0] = 0;
        for (int i = 1; i < _structure.Length; i++)
        {
            _layerOffsets[i] = _layerOffsets[i - 1] + _structure[i - 1];
        }

        int weightOffset = 0;
        int biasOffset = 0;
        for (int i = 0; i < _structure.Length - 1; i++)
        {
            _weightOffsets[i] = weightOffset;
            _biasOffsets[i] = biasOffset;
            weightOffset += _structure[i] * _structure[i + 1];
            biasOffset += _structure[i + 1];
        }
    }

    private void SyncFlatToMatrices()
    {
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            int weightStart = _weightOffsets[layer];
            int biasStart = _biasOffsets[layer];

            // Copy weights
            Matrix<double> matrix = _weights[layer];
            int idx = weightStart;
            for (int row = 0; row < matrix.RowCount; row++)
            {
                for (int col = 0; col < matrix.ColumnCount; col++)
                {
                    matrix[row, col] = _flatWeights[idx++];
                }
            }

            // Copy biases
            Vector<double> biasVector = _biases[layer];
            for (int i = 0; i < biasVector.Count; i++)
            {
                biasVector[i] = _flatBiases[biasStart + i];
            }
        }
    }

    private void SyncMatricesToFlat()
    {
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            int weightStart = _weightOffsets[layer];
            int biasStart = _biasOffsets[layer];

            // Copy weights
            Matrix<double> matrix = _weights[layer];
            int idx = weightStart;
            for (int row = 0; row < matrix.RowCount; row++)
            {
                for (int col = 0; col < matrix.ColumnCount; col++)
                {
                    _flatWeights[idx++] = matrix[row, col];
                }
            }

            // Copy biases
            Vector<double> biasVector = _biases[layer];
            for (int i = 0; i < biasVector.Count; i++)
            {
                _flatBiases[biasStart + i] = biasVector[i];
            }
        }
    }

    private void UpdateWeightsAdam(
        double learningRate = LearningRate,
        double beta1 = 0.9,
        double beta2 = 0.999,
        double epsilon = 1e-8)
    {
        _adamT++;
        double b1Corr = 1 - Math.Pow(beta1, _adamT);
        double b2Corr = 1 - Math.Pow(beta2, _adamT);

        for (int layer = 0; layer < _weights.Length; layer++)
        {
            // Weights
            _adamM[layer].Multiply(beta1, _adamM[layer]);
            _adamM[layer] += Gradients[layer] * (1 - beta1);
            _adamV[layer].Multiply(beta2, _adamV[layer]);
            _adamV[layer] += Gradients[layer].PointwisePower(2.0) * (1 - beta2);

            var mHat = _adamM[layer] / b1Corr;
            var vHat = _adamV[layer] / b2Corr;
            _weights[layer] -= mHat.PointwiseDivide(vHat.PointwiseSqrt() + epsilon) * learningRate;

            // Biases
            _adamMBias[layer].Multiply(beta1, _adamMBias[layer]);
            _adamMBias[layer] += GradientBiases[layer] * (1 - beta1);
            _adamVBias[layer].Multiply(beta2, _adamVBias[layer]);
            _adamVBias[layer] += GradientBiases[layer].PointwisePower(2.0) * (1 - beta2);

            var mHatB = _adamMBias[layer] / b1Corr;
            var vHatB = _adamVBias[layer] / b2Corr;
            _biases[layer] -= mHatB.PointwiseDivide(vHatB.PointwiseSqrt() + epsilon) * learningRate;
        }
    }
}