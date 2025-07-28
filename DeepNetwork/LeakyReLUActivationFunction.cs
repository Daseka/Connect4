namespace DeepNetwork;

[Serializable]
public class LeakyReLUActivationFunction : IActivationFunction
{
    private readonly double _alpha;
    public LeakyReLUActivationFunction(double alpha = 0.0001)
    {
        _alpha = alpha;
    }

    public double ShouldNormalize { get; } = 0;

    public double Calculate(double x)
    {
        // Leaky ReLU: f(x) = x if x > 0, else f(x) = alpha * x
        return x >= 0 ? x : _alpha * x;
    }

    public void Calculate(Span<double> x)
    {
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = x[i] >= 0 ? x[i] : _alpha * x[i]; 
        }
    }

    public double Derivative(double gradientValue)
    {
        return gradientValue >= 0 ? 1 : _alpha;
    }
}
