namespace Connect4.Ais;

public static class DirchletNoise
{
    public static void AddNoise(double[] policy, Random random, double epsilon = 0.25, double alpha = 0.3)
    {
        double[] noise = SampleDirichlet(policy.Length, alpha, random);

        for (int i = 0; i < policy.Length; i++)
        {
            policy[i] = (1 - epsilon) * policy[i] + epsilon * noise[i];
        }
    }

    private static double[] SampleDirichlet(int size, double alpha, Random random)
    {
        double[] samples = new double[size];
        double sum = 0.0;
        for (int i = 0; i < size; i++)
        {
            double x;
            do
            {
                x = -Math.Log(random.NextDouble());
            } while (x == 0.0);
            samples[i] = Math.Pow(x, alpha);
            sum += samples[i];
        }

        for (int i = 0; i < size; i++)
        {
            samples[i] /= sum;
        }

        return samples;
    }
}
