namespace DeepNetwork.ActivationFunctions;


[Serializable]
public class TanhActivationFunction : IActivationFunction
{
    public double ShouldNormalize { get; } = 0;

    public double Calculate(double x)
    {
        // Tanh activation function: (e^x - e^(-x)) / (e^x + e^(-x))
        return 2.0 / (1.0 + BoundedMath.Exp(-2.0 * x)) - 1.0;
    }

    public void Calculate(Span<double> x)
    {
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = 2.0 / (1.0 + BoundedMath.Exp(-2.0 * x[i])) - 1.0;
        }
    }

    public double Derivative(double gradientValue)
    {
        // The derivative of the tanh function is 1 - tanh(x)^2
        return 1 - gradientValue * gradientValue;
    }
}
