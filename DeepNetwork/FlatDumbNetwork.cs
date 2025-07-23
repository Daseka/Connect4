using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Numerics;

namespace DeepNetwork;

public class FlatDumbNetwork
{
    private const int MaximumStepSize = 50;
    private const double MinimumStepSize = 1e-6d;
    private const double NearNullValue = 1e-11d;
    private const double VariableLearnRateBig = 1.2d;
    private const double VariableLearnRateSmall = 0.5d;
    private const int CacheSize = 500000;

    public IActivationFunction[] ActivationFunctions;
    public double[] DeltaValues;
    public double[] Gradients;
    public double[] GradientBiases;
    public double[] PreviousGradients;
    public double[] PreviousWeightChange;
    public double[] Biases;
    public double[] PreviousBiases;
    public double[] PreviousBiasesChange;
    public bool Trained;
    public double[] UpdateValues;
    public double[] UpdateBiasesValues;
    public double[] Values;
    public double[] Weights;
    public int[] Structure;

    // Adam optimizer state
    private double[] AdamM;
    private double[] AdamV;
    private double[] AdamMBias;
    private double[] AdamVBias;
    private int AdamT;

    private ConcurrentDictionary<string, double[]> _cachedValues = new();
    private ConcurrentQueue<string> _cacheKeys = new();

    public double Error { get; set; }
    public double LastError { get; set; }

    public FlatDumbNetwork(IReadOnlyList<int> structure)
    {
        // Structure
        Structure = new int[structure.Count];
        Array.Copy(structure.ToArray(), Structure, structure.Count);

        // Activation functions
        ActivationFunctions = new IActivationFunction[structure.Count];
        for (int i = 0; i < structure.Count; i++)
        {
            ActivationFunctions[i] = i == structure.Count - 1
                ? new SigmoidActivationFunction()
            //    : new TanhActivationFunction();
            : new LeakyReLUActivationFunction();
        }

        // Value arrays
        int totalNeuronCount = structure.Sum();
        Values = new double[totalNeuronCount];
        DeltaValues = new double[totalNeuronCount];
        Biases = new double[totalNeuronCount - structure[0]];
        PreviousBiases = new double[totalNeuronCount - structure[0]];
        PreviousBiasesChange = new double[totalNeuronCount - structure[0]];
        UpdateBiasesValues = new double[totalNeuronCount - structure[0]];
        Array.Fill(UpdateBiasesValues, 0.001);

        // Weights arrays       
        int totalWeightCount = 0;
        for (int i = 0; i < structure.Count - 1; i++)
        {
            totalWeightCount += structure[i] * structure[i + 1];
        }

        Weights = new double[totalWeightCount];
        Gradients = new double[totalWeightCount];
        GradientBiases = new double[totalNeuronCount - structure[0]];
        PreviousGradients = new double[totalWeightCount];
        PreviousWeightChange = new double[totalWeightCount];
        UpdateValues = new double[totalWeightCount];
        Array.Fill(UpdateValues, 0.001);

        // Nguyen-Widrow initialization
        var random = new Random();
        int weightIndex = 0;
        for (int layer = 0; layer < structure.Count - 1; layer++)
        {
            int prevLayerSize = structure[layer];
            int nextLayerSize = structure[layer + 1];
            double beta = 0.7 * Math.Pow(nextLayerSize, 1.0 / prevLayerSize);

            for (int neuron = 0; neuron < nextLayerSize; neuron++)
            {
                double[] weightValue = new double[prevLayerSize];
                double norm = 0.0;
                for (int i = 0; i < prevLayerSize; i++)
                {
                    weightValue[i] = random.NextDouble() - 0.5;
                    norm += weightValue[i] * weightValue[i];
                }

                norm = Math.Sqrt(norm);
                for (int i = 0; i < prevLayerSize; i++)
                {
                    Weights[weightIndex++] = norm > 0 ? beta * weightValue[i] / norm : 0.0;
                }
            }
        }

        for (int i = 0; i < Biases.Length; i++)
        {
            Biases[i] = random.NextDouble() - 0.5;
        }

        AdamM = new double[Weights.Length];
        AdamV = new double[Weights.Length];
        AdamMBias = new double[Biases.Length];
        AdamVBias = new double[Biases.Length];
        AdamT = 0;
    }

    public double[] Calculate(double[] input)
    {
        Buffer.BlockCopy(input, 0, Values, 0, input.Length * sizeof(double));

        int layerStart = 0;
        int weightIndex = 0;
        int biasIndex = 0; 
        for (int layer = 1; layer < Structure.Length; layer++)
        {
            int prevLayerSize = Structure[layer - 1];
            int currLayerSize = Structure[layer];
            int currLayerStart = layerStart + prevLayerSize;

            var prevValues = Values.AsSpan(layerStart, prevLayerSize);
            var currValues = Values.AsSpan(currLayerStart, currLayerSize);

            for (int node = 0; node < currLayerSize; node++)
            {
                var weights = Weights.AsSpan(weightIndex, prevLayerSize);
                double weightedSum = DotProduct(prevValues, weights) + Biases[biasIndex];
                currValues[node] = ActivationFunctions[layer].Calculate(weightedSum);
                weightIndex += prevLayerSize;
                biasIndex++;
            }

            layerStart = currLayerStart;
        }

        int lastLayerStart = Values.Length - Structure[^1];
        return [.. Values.Skip(lastLayerStart).Take(Structure[^1])];
    }

    private static double DotProduct(ReadOnlySpan<double> values, ReadOnlySpan<double> weights)
    {
        int i = 0;
        double sum = 0.0;

        if (Vector.IsHardwareAccelerated)
        {
            int simdLength = Vector<double>.Count;
            Vector<double> vsum = Vector<double>.Zero;
            for (; i <= values.Length - simdLength; i += simdLength)
            {
                var va = new Vector<double>(values.Slice(i, simdLength));
                var vb = new Vector<double>(weights.Slice(i, simdLength));
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

    public (double[] gradients, double[] biases, double error) ComputeGradientsAndError(double[][] trainingInputs, double[][] trainingOutputs)
    {
        Array.Clear(Gradients, 0, Gradients.Length);
        Array.Clear(GradientBiases, 0, GradientBiases.Length);
        double error = 0.0;
        int setCount = 0;

        int numLayers = Structure.Length;
        int[] layerOffsets = new int[numLayers];
        layerOffsets[0] = 0;
        for (int l = 1; l < numLayers; l++)
        {
            layerOffsets[l] = layerOffsets[l - 1] + Structure[l - 1];
        }

        for (int sample = 0; sample < trainingInputs.Length; sample++)
        {
            // Forward pass
            double[] _ = Calculate(trainingInputs[sample]);

            // Compute output layer deltas
            int lastLayer = numLayers - 1;
            int lastLayerStart = layerOffsets[lastLayer];
            for (int node = 0; node < Structure[lastLayer]; node++)
            {
                double outVal = Values[lastLayerStart + node];
                double target = trainingOutputs[sample][node];
                DeltaValues[lastLayerStart + node] = (target - outVal) * ActivationFunctions[lastLayer].Derivative(outVal);

                double diff = target - outVal;
                error += diff * diff;
                setCount++;
            }

            // Backpropagate deltas
            for (int layer = numLayers - 2; layer >= 1; layer--)
            {
                int layerStart = layerOffsets[layer];
                int nextLayerStart = layerOffsets[layer + 1];
                int currLayerSize = Structure[layer];
                int nextLayerSize = Structure[layer + 1];

                int weightBase = 0;
                for (int l = 0; l < layer; l++)
                {
                    weightBase += Structure[l] * Structure[l + 1];
                }

                for (int node = 0; node < currLayerSize; node++)
                {
                    double sum = 0.0;
                    for (int nextNode = 0; nextNode < nextLayerSize; nextNode++)
                    {
                        int wIdx = weightBase + currLayerSize * nextNode + node;
                        sum += Weights[wIdx] * DeltaValues[nextLayerStart + nextNode];
                    }
                    double val = Values[layerStart + node];
                    DeltaValues[layerStart + node] = sum * ActivationFunctions[layer].Derivative(val);
                }
            }

            // Accumulate gradients
            int weightIndex = 0;
            for (int layer = 1; layer < numLayers; layer++)
            {
                int prevLayerStart = layerOffsets[layer - 1];
                int currLayerStart = layerOffsets[layer];
                int prevLayerSize = Structure[layer - 1];
                int currLayerSize = Structure[layer];

                for (int node = 0; node < currLayerSize; node++)
                {
                    for (int prevNode = 0; prevNode < prevLayerSize; prevNode++)
                    {
                        Gradients[weightIndex] += DeltaValues[currLayerStart + node] * Values[prevLayerStart + prevNode];
                        weightIndex++;
                    }
                }
            }

            // Accumulate biases gradients
            for (int layer = 1; layer < numLayers; layer++)
            {
                int layerStart = layerOffsets[layer];
                int layerSize = Structure[layer];
                int biasIndex = 0;
                for (int node = 0; node < layerSize; node++)
                {
                    GradientBiases[biasIndex] += DeltaValues[layerStart + node];
                    biasIndex++;
                }
            }
        }

        error = Math.Sqrt(error / setCount);

        return (Gradients, GradientBiases, error);
    }

    public static FlatDumbNetwork? CreateFromFile(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return null;
        }

        string json = File.ReadAllText(fileName);
        var jObject = JObject.Parse(json);
        
        int[] structure = jObject[nameof(Structure)]!.ToObject<int[]>()!;
        var network = new FlatDumbNetwork(structure)
        {
            DeltaValues = jObject[nameof(DeltaValues)]!.ToObject<double[]>()!,
            Gradients = jObject[nameof(Gradients)]!.ToObject<double[]>()!,
            PreviousGradients = jObject[nameof(PreviousGradients)]!.ToObject<double[]>()!,
            PreviousWeightChange = jObject[nameof(PreviousWeightChange)]!.ToObject<double[]>()!,
            UpdateValues = jObject[nameof(UpdateValues)]!.ToObject<double[]>()!,
            Values = jObject[nameof(Values)]!.ToObject<double[]>()!,
            Weights = jObject[nameof(Weights)]!.ToObject<double[]>()!,
            Biases = jObject[nameof(Biases)]!.ToObject<double[]>()!,
            PreviousBiases = jObject[nameof(PreviousBiases)]!.ToObject<double[]>()!,
            PreviousBiasesChange = jObject[nameof(PreviousBiasesChange)]!.ToObject<double[]>()!,
            UpdateBiasesValues = jObject[nameof(UpdateBiasesValues)]!.ToObject<double[]>()!,
            Trained = true
        };

        // Restore activation functions
        string[] activationFunctionTypes = jObject["ActivationFunctionTypes"]!.ToObject<string[]>()!;
        for (int i = 0; i < activationFunctionTypes.Length; i++)
        {
            network.ActivationFunctions[i] = activationFunctionTypes[i] switch
            { 
                nameof(SigmoidActivationFunction) => new SigmoidActivationFunction(),
                nameof(LeakyReLUActivationFunction) => new LeakyReLUActivationFunction(),
                nameof(TanhActivationFunction) => new TanhActivationFunction(),
                _ => throw new InvalidOperationException($"Unknown activation function: {activationFunctionTypes[i]}")
            };
        }

        return network;
    }

    public double[] CalculateCached(string id, double[] input)
    {
        if (_cachedValues.TryGetValue(id, out double[]? cachedResult))
        {
            return cachedResult;
        }

        double[] result = Calculate(input);
        _cachedValues[id] = result;

        _cacheKeys.Enqueue(id);
        while (_cachedValues.Count > CacheSize)
        {
            if (_cacheKeys.TryDequeue(out string? oldestKey))
            {
                _cachedValues.TryRemove(oldestKey, out _);
            }
        }

        return result;
    }

    public void ClearCache()
    {
        _cachedValues.Clear();
        _cacheKeys.Clear();
    }

    public FlatDumbNetwork Clone()
    {
        var clone = new FlatDumbNetwork(Structure)
        {
            ActivationFunctions = (IActivationFunction[])ActivationFunctions.Clone(),
            DeltaValues = (double[])DeltaValues.Clone(),
            Gradients = (double[])Gradients.Clone(),
            PreviousGradients = (double[])PreviousGradients.Clone(),
            PreviousWeightChange = (double[])PreviousWeightChange.Clone(),
            Trained = Trained,
            UpdateValues = (double[])UpdateValues.Clone(),
            Values = (double[])Values.Clone(),
            Weights = (double[])Weights.Clone(),
            Structure = (int[])Structure.Clone(),
            Biases = (double[])Biases.Clone(),
            PreviousBiases = (double[])PreviousBiases.Clone(),
            PreviousBiasesChange = (double[])PreviousBiasesChange.Clone(),
            UpdateBiasesValues = (double[])UpdateBiasesValues.Clone(),
        };

        return clone;
    }

    public void SaveToFile(string fileName)
    {
        // Serialize activation function types as strings
        string[] activationFunctionTypes = [.. ActivationFunctions.Select(f => f.GetType().Name)];

        var wrapper = new
        {
            DeltaValues,
            Gradients,
            PreviousGradients,
            PreviousWeightChange,
            UpdateValues,
            Values,
            Weights,
            Structure,
            Biases,
            PreviousBiases,
            PreviousBiasesChange,
            UpdateBiasesValues,
            ActivationFunctionTypes = activationFunctionTypes,
        };

        string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
        File.WriteAllText(fileName, json);
    }

    public void UpdateWeights()
    {
        for (int i = 0; i < Weights.Length; i++)
        {
            double grad = Gradients[i];
            double prevGrad = PreviousGradients[i];
            double step = UpdateValues[i];
            double lastChange = PreviousWeightChange[i];

            int sign = Math.Abs(grad * prevGrad) < NearNullValue ? 0 : Math.Sign(grad * prevGrad);

            double weightChange = 0.0;

            if (sign > 0)
            {
                // Same direction: increase step size
                step = Math.Min(step * VariableLearnRateBig, MaximumStepSize);
                weightChange = Math.Sign(grad) * step;
                Weights[i] += weightChange;
                PreviousGradients[i] = grad;
            }
            else if (sign < 0)
            {
                // Opposite direction: decrease step size, revert last change
                step = Math.Max(step * VariableLearnRateSmall, MinimumStepSize);
                // Undo last weight change
                Weights[i] -= lastChange;
                PreviousGradients[i] = 0;
            }
            else // sign == 0
            {
                // No change: use current step size
                weightChange = Math.Sign(grad) * step;
                Weights[i] += weightChange;
                PreviousGradients[i] = grad;
            }

            UpdateValues[i] = step;
            PreviousWeightChange[i] = weightChange;
        }

        for (int i = 0; i < Biases.Length; i++)
        {
            double grad = GradientBiases[i];
            double prevGrad = PreviousBiases[i];
            double step = UpdateBiasesValues[i];
            double lastChange = PreviousBiasesChange[i];
            int sign = Math.Abs(grad * prevGrad) < NearNullValue ? 0 : Math.Sign(grad * prevGrad);
            double biasChange = 0.0;
            if (sign > 0)
            {
                // Same direction: increase step size
                step = Math.Min(step * VariableLearnRateBig, MaximumStepSize);
                biasChange = Math.Sign(grad) * step;
                Biases[i] += biasChange;
                PreviousBiases[i] = grad;
            }
            else if (sign < 0)
            {
                // Opposite direction: decrease step size, revert last change
                step = Math.Max(step * VariableLearnRateSmall, MinimumStepSize);
                // Undo last bias change
                Biases[i] -= lastChange;
                PreviousBiases[i] = 0;
            }
            else // sign == 0
            {
                // No change: use current step size
                biasChange = Math.Sign(grad) * step;
                Biases[i] += biasChange;
                PreviousBiases[i] = grad;
            }

            UpdateBiasesValues[i] = step;
            PreviousBiasesChange[i] = biasChange;
        }

    }
    
    public void UpdateWeightsAdam(double learningRate = 0.001, double beta1 = 0.9, double beta2 = 0.999, double epsilon = 1e-8)
    {
        AdamT++;
        // Update weights
        for (int i = 0; i < Weights.Length; i++)
        {
            // Update biased first moment estimate
            AdamM[i] = beta1 * AdamM[i] + (1 - beta1) * Gradients[i];
            // Update biased second raw moment estimate
            AdamV[i] = beta2 * AdamV[i] + (1 - beta2) * (Gradients[i] * Gradients[i]);
            // Compute bias-corrected first moment estimate
            double mHat = AdamM[i] / (1 - Math.Pow(beta1, AdamT));
            // Compute bias-corrected second raw moment estimate
            double vHat = AdamV[i] / (1 - Math.Pow(beta2, AdamT));
            // Update parameter
            Weights[i] += learningRate * mHat / (Math.Sqrt(vHat) + epsilon);
        }
        // Update biases
        for (int i = 0; i < Biases.Length; i++)
        {
            AdamMBias[i] = beta1 * AdamMBias[i] + (1 - beta1) * GradientBiases[i];
            AdamVBias[i] = beta2 * AdamVBias[i] + (1 - beta2) * (GradientBiases[i] * GradientBiases[i]);
            double mHat = AdamMBias[i] / (1 - Math.Pow(beta1, AdamT));
            double vHat = AdamVBias[i] / (1 - Math.Pow(beta2, AdamT));
            Biases[i] += learningRate * mHat / (Math.Sqrt(vHat) + epsilon);
        }
    }
}
