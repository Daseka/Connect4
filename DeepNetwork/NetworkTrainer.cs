namespace DeepNetwork;

public class NetworkTrainer
{
    private readonly SimpleDumbNetwork _network;

    public NetworkTrainer(SimpleDumbNetwork network)
    {
        _network = network;
    }

    public double Train(double[][] trainingInputs, double[][] trainingOutputs)
    {
        _network.Error = 0;

        int total = trainingInputs.Length;
        int threads = GetSafeThreadCount(total);

        int chunkSize = total / threads;
        int remainder = total % threads;
        double[][][][] gradientsPerThread = new double[threads][][][];
        double[] errors = new double[threads];
        int[] counts = new int[threads];

        // Calculate al the gradients in parallell
        _ = Parallel.For(0, threads, t =>
        {
            int start = t * chunkSize + Math.Min(t, remainder);
            int end = start + chunkSize + (t < remainder ? 1 : 0);
            int count = end - start;
            counts[t] = count;

            double[][] inputsPart = new double[count][];
            double[][] outputsPart = new double[count][];
            Array.Copy(trainingInputs, start, inputsPart, 0, count);
            Array.Copy(trainingOutputs, start, outputsPart, 0, count);

            SimpleDumbNetwork clone = _network.Clone();
            (gradientsPerThread[t], errors[t]) = clone.ComputeGradientsAndError(inputsPart, outputsPart);
        });

        // add all the gradients together and devide them by all the threads
        for (int i = 0; i < _network.Gradients.Length; i++)
        {
            for (int j = 0; j < _network.Gradients[i].Length; j++)
            {
                for (int k = 0; k < _network.Gradients[i][j].Length; k++)
                {
                    double sum = 0;
                    for (int t = 0; t < threads; t++)
                    {
                        sum += gradientsPerThread[t][i][j][k];
                    }

                    _network.Gradients[i][j][k] = sum / threads;
                }
            }
        }

        // Average error
        double totalError = 0;
        for (int t = 0; t < threads; t++)
        {
            totalError += errors[t];
        }

        _network.Error = totalError / threads;

        _network.UpdateWeights();

        _network.LastError = _network.Error;

        return _network.Error;
    }

    private int GetSafeThreadCount(int trainingInputCount)
    {
        if (trainingInputCount < 100)
        {
            return 1;
        }

        int maxThreadsByWorkload = trainingInputCount / 100;
        int threadCount = Math.Min(Environment.ProcessorCount, trainingInputCount);

        if (maxThreadsByWorkload > 0)
        {
            threadCount = Math.Min(threadCount, maxThreadsByWorkload);
        }

        return Math.Max(1, threadCount);
    }
}
