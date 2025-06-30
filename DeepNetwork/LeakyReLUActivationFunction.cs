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
        return x >= 0 ? x : _alpha * x;
    }

    public double Derivative(double gradientValue)
    {
        return gradientValue >= 0 ? 1 : _alpha;
    }
}
