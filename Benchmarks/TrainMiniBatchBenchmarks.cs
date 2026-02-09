using BenchmarkDotNet.Attributes;

namespace DeepNetwork.Benchmarks;

[MemoryDiagnoser]
public class TrainMiniBatchBenchmarks
{
    private MiniBatchMatrixNetwork _network = null!;
    private double[][] _inputs = null!;
    private double[][] _targets = null!;
    private int _inputSize;
    private int _outputSize;

    [Params(64, 128, 256, 512, 1024, 2048, 4096, 8192)]
    public int BatchSize { get; set; }

    [Params(200000)]
    public int SampleCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        int[] structure = [127, 256, 128, 64, 7];
        _network = new MiniBatchMatrixNetwork(structure, isSoftmax: true);
        _inputSize = structure[0];
        _outputSize = structure[^1];
        _inputs = CreateInputSamples(SampleCount, _inputSize);
        _targets = CreateTargetSamples(SampleCount, _outputSize);
    }

    [Benchmark]
    public double Train()
    {
        return _network.TrainMiniBatch(_inputs, _targets, BatchSize);
    }

    private static double[][] CreateInputSamples(int sampleCount, int width)
    {
        var random = new Random(1234);
        double[][] data = new double[sampleCount][];
        for (int i = 0; i < sampleCount; i++)
        {
            double[] sample = new double[width];
            for (int j = 0; j < width; j++)
            {
                sample[j] = random.NextDouble() - 0.5;
            }

            data[i] = sample;
        }

        return data;
    }

    private static double[][] CreateTargetSamples(int sampleCount, int width)
    {
        var random = new Random(5678);
        double[][] targets = new double[sampleCount][];
        for (int i = 0; i < sampleCount; i++)
        {
            double[] sample = new double[width];
            int hotIndex = random.Next(width);
            sample[hotIndex] = 1.0;
            targets[i] = sample;
        }

        return targets;
    }
}
