namespace DeepNetwork;

[Serializable]
public class SoftMaxActivationFunction : IActivationFunction
{
    public double ShouldNormalize { get; } = 0;

    /// <summary>
    /// SoftMax doesnt work with single value input only whole layer
    /// </summary>
    public double Calculate(double x)
    {
        throw new NotImplementedException();
    }

    public void Calculate(Span<double> x)
    {
        double max = x[0];
        for (int i = 1; i < x.Length; i++)
        {
            if (x[i] > max)
            {
                max = x[i];
            }
        }

        double sum = 0.0;
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = BoundedMath.Exp(x[i] - max);
            sum += x[i];
        }

        if (sum == 0)
        {
            sum = 1; // Prevent division by zero
        }

        for (int i = 0; i < x.Length; i++)
        {
            x[i] /= sum ;
        }
    }

    public double Derivative(double gradientValue)
    {
        return 1; 
    }
}
