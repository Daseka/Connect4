namespace DeepNetwork;

public interface IActivationFunction
{
    double Calculate(double x);
    double Derivative(double gradientValue);

    double ShouldNormalize { get; }
}
