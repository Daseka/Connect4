namespace DeepNetwork.ActivationFunctions;

[Serializable]
public class SigmoidActivationFunction : IActivationFunction
{
    public double ShouldNormalize { get; } = 1;

    public double Calculate(double x)
    {
        // Sigmoid activation function: 1 / (1 + e^(-x))
        return 1.0 / (1.0 + BoundedMath.Exp(-x));
    }

    public void Calculate(Span<double> x)
    {
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = 1.0 / (1.0 + BoundedMath.Exp(-x[i]));
        }
    }

    public double Derivative(double gradientValue)
    {
        // The derivative of the sigmoid function is sigmoid(x) * (1 - sigmoid(x))
        return gradientValue * (1 - gradientValue);
    }
}
