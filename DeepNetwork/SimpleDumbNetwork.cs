using MathNet.Numerics.LinearAlgebra;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeepNetwork;

[Serializable]
public class SimpleDumbNetwork
{
    public bool Trained;
    public IActivationFunction[] ActivationFunctions;
    public double[][] DeltaValues;
    public double[][][] Gradients;
    public double[][][] PreviousGradients;
    public double[][][] PreviousWeightChange;
    public double[][][] UpdateValues;
    public double[][] Values;
    public double[][][] Weights;
    private const int MaximumStepSize = 50;
    private const double MinimumStepSize = 1e-6d;
    private const double NearNullValue = 1e-11d;
    private const double VariableLearnRateBig = 1.2d;
    private const double VariableLearnRateSmall = 0.5d;
    private static readonly Random _random = new();

    public double Error { get; set; }
    public double LastError { get; set; }

    public SimpleDumbNetwork(IReadOnlyList<int> structure)
    {
        ActivationFunctions = new IActivationFunction[structure.Count];

        Values = new double[structure.Count][];
        DeltaValues = new double[structure.Count][];

        // Weights arrays describe the connections between layers.
        // the arrays are [LayerInbetweenValueLayers][NodeInValueLayer + 1][Weight]
        Weights = new double[structure.Count - 1][][];
        Gradients = new double[structure.Count - 1][][];
        PreviousGradients = new double[structure.Count - 1][][];
        PreviousWeightChange = new double[structure.Count - 1][][];
        UpdateValues = new double[structure.Count - 1][][];

        for (int i = 0; i < structure.Count; i++)
        {
            ActivationFunctions[i] = i == structure.Count - 1
                ? new SigmoidActivationFunction()
                : new TanhActivationFunction();
            //: new LeakyReLUActivationFunction();

            Values[i] = new double[structure[i]];
            DeltaValues[i] = new double[structure[i]];
        }

        for (int i = 0; i < structure.Count - 1; i++)
        {
            Weights[i] = new double[Values[i + 1].Length][];
            Gradients[i] = new double[Values[i + 1].Length][];
            PreviousGradients[i] = new double[Values[i + 1].Length][];
            PreviousWeightChange[i] = new double[Values[i + 1].Length][];
            UpdateValues[i] = new double[Values[i + 1].Length][];

            int neuronCount = Values[i].Length;
            int nextNeuronCount = Values[i + 1].Length;
            double beta = 0.7 * Math.Pow(nextNeuronCount, 1.0 / neuronCount);

            for (int j = 0; j < Weights[i].Length; j++)
            {
                Weights[i][j] = new double[neuronCount];
                Gradients[i][j] = new double[neuronCount];
                PreviousGradients[i][j] = new double[neuronCount];
                PreviousWeightChange[i][j] = new double[neuronCount];
                UpdateValues[i][j] = new double[neuronCount];

                // Nguyen-Widrow: random in [-0.5, 0.5]
                double norm = 0.0;
                for (int k = 0; k < neuronCount; k++)
                {
                    Weights[i][j][k] = _random.NextDouble() - 0.5;
                    norm += Weights[i][j][k] * Weights[i][j][k];
                    UpdateValues[i][j][k] = 0.1;
                }
                norm = Math.Sqrt(norm);
                if (norm > 0)
                {
                    for (int k = 0; k < neuronCount; k++)
                    {
                        Weights[i][j][k] = beta * Weights[i][j][k] / norm;
                    }
                }
            }
        }
    }

    public static SimpleDumbNetwork? CreateFromFile(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return null;
        }

        string json = File.ReadAllText(fileName);
        var jObject = JObject.Parse(json);

        // Read structure from Values array
        double[][] valuesArray = jObject[nameof(Values)]!.ToObject<double[][]>()!;
        int[] structure = [.. valuesArray.Select(arr => arr.Length)];

        var network = new SimpleDumbNetwork(structure)
        {
            DeltaValues = jObject[nameof(DeltaValues)]!.ToObject<double[][]>(),
            Gradients = jObject[nameof(Gradients)]!.ToObject<double[][][]>(),
            PreviousGradients = jObject[nameof(PreviousGradients)]!.ToObject<double[][][]>(),
            PreviousWeightChange = jObject[nameof(PreviousWeightChange)]!.ToObject<double[][][]>(),
            UpdateValues = jObject[nameof(UpdateValues)]!.ToObject<double[][][]>(),
            Values = valuesArray,
            Weights = jObject[nameof(Weights)]!.ToObject<double[][][]>(),
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

    public double[] Calculate(double[] input)
    {
        for (int node = 0; node < Values[0].Length; node++)
        {
            Values[0][node] = input[node];
        }

        for (int layer = 1; layer < Values.Length; layer++)
        {
            for (int node = 0; node < Values[layer].Length; node++)
            {
                int previousLayer = layer - 1;
                double sum = Sum(Values[previousLayer], Weights[previousLayer][node]);
                Values[layer][node] = ActivationFunctions[layer].Calculate(sum);
            }
        }

        return Values[^1];

        //for (int node = 0; node < Values[0].Length; node++)
        //{
        //    Values[0][node] = input[node];
        //}

        //for (int layer = 1; layer < Values.Length; layer++)
        //{
        //    var inputVector = Vector<double>.Build.DenseOfArray(Values[layer - 1]);
        //    var weightMatrix = Matrix<double>.Build.DenseOfRowArrays(Weights[layer - 1]);
        //    var sums = weightMatrix * inputVector;

        //    for (int node = 0; node < Values[layer].Length; node++)
        //    {
        //        Values[layer][node] = ActivationFunctions[layer].Calculate(sums[node]);
        //    }
        //}

        //return [.. Values[^1]];
    }

    private static double Sum(double[] values, IReadOnlyList<double> weights)
    {
        double result = 0;
        for (int i = 0; i < values.Length; i++)
        {
            result += values[i] * weights[i];
        }

        return result;
    }

    public SimpleDumbNetwork Clone()
    {
        // Create a new instance with the same structure
        int[] structure = Values.Select(v => v.Length).ToArray();
        var clone = new SimpleDumbNetwork(structure);

        for (int i = 0; i < Values.Length; i++)
        {
            Array.Copy(Values[i], clone.Values[i], Values[i].Length);
            Array.Copy(DeltaValues[i], clone.DeltaValues[i], DeltaValues[i].Length);
        }

        for (int i = 0; i < Weights.Length; i++)
        {
            for (int j = 0; j < Weights[i].Length; j++)
            {
                Array.Copy(Weights[i][j], clone.Weights[i][j], Weights[i][j].Length);
                Array.Copy(Gradients[i][j], clone.Gradients[i][j], Gradients[i][j].Length);
                Array.Copy(PreviousGradients[i][j], clone.PreviousGradients[i][j], PreviousGradients[i][j].Length);
                Array.Copy(PreviousWeightChange[i][j], clone.PreviousWeightChange[i][j], PreviousWeightChange[i][j].Length);
                Array.Copy(UpdateValues[i][j], clone.UpdateValues[i][j], UpdateValues[i][j].Length);
            }
        }

        clone.Error = Error;
        clone.LastError = LastError;

        return clone;
    }

    public (double[][][] gradients, double error) ComputeGradientsAndError(double[][] trainingInputs, double[][] trainingOutputs)
    {
        int setCount = 0;

        for (int i = 0; i < trainingInputs.Length; i++)
        {
            //Set values using training data
            _ = Calculate(trainingInputs[i]);

            ////Calculate Gradients
            //var output = Vector<double>.Build.DenseOfArray(Values[^1]);
            //var target = Vector<double>.Build.DenseOfArray(trainingOutputs[i]);
            //var deltaV = (target - output).PointwiseMultiply(output.Map(ActivationFunctions[^1].Derivative));
            //DeltaValues[^1] = [.. deltaV];

            //double[][] dvals = new double[DeltaValues.Length][];
            //for (int layer = Values.Length - 2; layer >= 1; layer--)
            //{
            //    var weights = Matrix<double>.Build.DenseOfRowArrays(Weights[layer]);
            //    var nextDelta = Vector<double>.Build.DenseOfArray(DeltaValues[layer + 1]);
            //    var value = Vector<double>.Build.DenseOfArray(Values[layer]);
            //    // Multiply weights^T * nextDelta
            //    var sum = weights.TransposeThisAndMultiply(nextDelta);
            //    var deltavalues = sum.PointwiseMultiply(value.Map(ActivationFunctions[layer].Derivative));
            //    DeltaValues[layer] = [.. deltavalues];
            //}

            //for (int layer = 0; layer < Weights.Length; layer++)
            //{
            //    var delta = Vector<double>.Build.DenseOfArray(DeltaValues[layer + 1]);
            //    var prevValues = Vector<double>.Build.DenseOfArray(Values[layer]);
            //    var grad = delta.OuterProduct(prevValues); // Matrix: [node][prevNode]
            //    for (int node = 0; node < Gradients[layer].Length; node++)
            //        for (int prevNode = 0; prevNode < Gradients[layer][node].Length; prevNode++)
            //            Gradients[layer][node][prevNode] += grad[node, prevNode];
            //}

            // Calculate Gradients
            for (int node = 0; node < DeltaValues[^1].Length; node++)
            {
                double output = Values[^1][node];
                double target = trainingOutputs[i][node];
                DeltaValues[^1][node] = (target - output) * ActivationFunctions[^1].Derivative(output);
            }

            for (int layer = Values.Length - 2; layer >= 1; layer--)
            {
                for (int node = 0; node < Values[layer].Length; node++)
                {
                    double sum = 0;
                    for (int nextNode = 0; nextNode < Values[layer + 1].Length; nextNode++)
                    {
                        sum += Weights[layer][nextNode][node] * DeltaValues[layer + 1][nextNode];
                    }

                    double value = Values[layer][node];

                    DeltaValues[layer][node] = sum * ActivationFunctions[layer].Derivative(value);
                }
            }

            for (int layer = 0; layer < Weights.Length; layer++)
            {
                for (int node = 0; node < Weights[layer].Length; node++)
                {
                    for (int prevNode = 0; prevNode < Weights[layer][node].Length; prevNode++)
                    {
                        Gradients[layer][node][prevNode] += DeltaValues[layer + 1][node] * Values[layer][prevNode];
                    }
                }
            }

            // Update error
            double[] desired = trainingOutputs[i];
            double[] actual = Values[^1];
            for (int j = 0; j < desired.Length; j++)
            {
                double delta = desired[j] - actual[j];
                Error += delta * delta;
                setCount++;
            }
        }

        Error = Math.Sqrt(Error / setCount);
        LastError = Error;

        return (Gradients, Error);
    }

    public void SaveToFile(string fileName)
    {
        // Serialize activation function types as strings
        string[] activationFunctionTypes = ActivationFunctions
            .Select(f => f.GetType().Name)
            .ToArray();

        var wrapper = new
        {
            DeltaValues,
            Gradients,
            PreviousGradients,
            PreviousWeightChange,
            UpdateValues,
            Values,
            Weights,
            ActivationFunctionTypes = activationFunctionTypes,
        };

        string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
        File.WriteAllText(fileName, json);
    }

    public void UpdateWeights()
    {
        // Update weights
        for (int i = 0; i < Gradients.Length; i++)
        {
            for (int j = 0; j < Gradients[i].Length; j++)
            {
                for (int k = 0; k < Gradients[i][j].Length; k++)
                {
                    Weights[i][j][k] += UpdateWeight(
                        Gradients[i][j],
                        PreviousGradients[i][j],
                        PreviousWeightChange[i][j],
                        UpdateValues[i][j], k);

                    Gradients[i][j][k] = 0;
                }
            }
        }
    }

    private static int GetStepDirection(double value)
    {
        return value > 0
            ? 1
            : -1;
    }

    private static int Sign(double value)
    {
        return Math.Abs(value) < NearNullValue ? 0 : value > 0 ? 1 : -1;
    }

    private double UpdateWeight(
        double[] gradients,
        double[] lastGradient,
        double[] _lastWeightChanged,
        double[] updateValues,
        int index)
    {
        int change = Sign(gradients[index] * lastGradient[index]);
        double weightChange = 0;

        // if change positive... can take a big step
        if (change > 0)
        {
            double delta = Math.Min(updateValues[index] * VariableLearnRateBig, MaximumStepSize);
            weightChange = GetStepDirection(gradients[index]) * delta;

            updateValues[index] = delta;
            lastGradient[index] = gradients[index];
        }
        // if change negative... must go back and take a small step
        else if (change < 0)
        {
            double delta = updateValues[index] * VariableLearnRateSmall;
            delta = Math.Max(delta, MinimumStepSize);
            updateValues[index] = delta;

            if (Error > LastError)
            {
                weightChange = -_lastWeightChanged[index];
            }

            lastGradient[index] = 0;
        }

        // if no change then make normal step
        else if (change == 0)
        {
            double delta = updateValues[index];
            weightChange = GetStepDirection(gradients[index]) * delta;

            lastGradient[index] = gradients[index];
        }

        _lastWeightChanged[index] = weightChange;

        return weightChange;
    }
}