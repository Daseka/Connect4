namespace DeepNetwork;

public class MiniBatchNetworkTrainer : INetworkTrainer
{
    public const int BatchSize = 8192;
    //public const int BatchSize = 4096;

    private readonly MiniBatchMatrixNetwork _network;
    
    public MiniBatchNetworkTrainer(IStandardNetwork network)
    {
        if (network is not MiniBatchMatrixNetwork miniBatchNetwork)
        {
            throw new ArgumentException($"Network must be of type MiniBatchMatrixNetwork not {network.GetType().Name}", nameof(network));
        }
        _network = miniBatchNetwork;
        //_network.ResetAdamTimer();
    }
    
    public double Train(double[][] trainingInputs, double[][] trainingOutputs, double? learnRate)
    {
        if (trainingInputs.Length == 0)
        {
            return 0;     
        }

        int effectiveBatchSize = CalculateEffectiveBatchSize(trainingInputs.Length);

        double error = learnRate.HasValue
            ? _network.TrainMiniBatch(trainingInputs, trainingOutputs, effectiveBatchSize, learnRate.Value)
            : _network.TrainMiniBatch(trainingInputs, trainingOutputs, effectiveBatchSize);

        _network.Trained = true;
        _network.ClearCache();

        return error;
    }

    private static int CalculateEffectiveBatchSize(int sampleCount)
    {
        if (sampleCount <= BatchSize)
        {
            return Math.Max(32, sampleCount);
        }

        int desiredBatches = Math.Max(1, Environment.ProcessorCount);
        int sizeForTargetBatches = (sampleCount + desiredBatches - 1) / desiredBatches;
        return Math.Clamp(sizeForTargetBatches, 32, BatchSize);
    }
}
