namespace DeepNetwork.ActivationFunctions;

public interface IActivationFunction
{
    double Calculate(double x);

    void Calculate(Span<double> x);

    double Derivative(double gradientValue);

    double ShouldNormalize { get; }
}
