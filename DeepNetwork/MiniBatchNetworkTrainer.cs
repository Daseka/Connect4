namespace DeepNetwork;

public class MiniBatchNetworkTrainer : INetworkTrainer
{
    public const int BatchSize = 1024;
    private readonly MiniBatchMatrixNetwork _network;
    
    public MiniBatchNetworkTrainer(IStandardNetwork network)
    {
        if (network is not MiniBatchMatrixNetwork miniBatchNetwork)
        {
            throw new ArgumentException($"Network must be of type MiniBatchMatrixNetwork not {network.GetType().Name}", nameof(network));
        }
        _network = miniBatchNetwork;
    }
    
    public double Train(double[][] trainingInputs, double[][] trainingOutputs)
    {
        if (trainingInputs.Length == 0)
        {
            return 0;     
        }

        double error = _network.TrainMiniBatch(trainingInputs, trainingOutputs, BatchSize);
        _network.Trained = true;
        _network.ClearCache();

        return error;
    }
}
