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
    public double[] PreviousGradients;
    public double[] PreviousWeightChange;
    public bool Trained;
    public double[] UpdateValues;
    public double[] Values;
    public double[] Weights;
    public int[] Structure;

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

        // Weights arrays       
        int totalWeightCount = 0;
        for (int i = 0; i < structure.Count - 1; i++)
        {
            totalWeightCount += structure[i] * structure[i + 1];
        }

        Weights = new double[totalWeightCount];
        Gradients = new double[totalWeightCount];
        PreviousGradients = new double[totalWeightCount];
        PreviousWeightChange = new double[totalWeightCount];
        UpdateValues = new double[totalWeightCount];
        Array.Fill(UpdateValues, 0.1);

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
    }

    public double[] Calculate(double[] input)
    {
        Buffer.BlockCopy(input, 0, Values, 0, input.Length * sizeof(double));

        int layerStart = 0;
        int weightIndex = 0;
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
                currValues[node] = ActivationFunctions[layer].Calculate(
                    DotProduct(prevValues, weights)
                );
                weightIndex += prevLayerSize;
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

    public (double[] gradients, double error) ComputeGradientsAndError(double[][] trainingInputs, double[][] trainingOutputs)
    {
        Array.Clear(Gradients, 0, Gradients.Length);
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
                int prevLayerSize = Structure[layer - 1];
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
        }

        error = Math.Sqrt(error / setCount);

        return (Gradients, error);
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
    }
}
