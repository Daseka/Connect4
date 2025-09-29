namespace DeepNetwork;

public class NetworkTrainerFactory
{
    public static INetworkTrainer CreateNetworkTrainer(IStandardNetwork? network)
    {
        if (network is FlatDumbNetwork flatNetwork)
        {
            return new FlatNetworkTrainer(flatNetwork);
        }

        if (network is MiniBatchMatrixNetwork miniBatchNetwork)
        {
            return new MiniBatchNetworkTrainer(miniBatchNetwork);
        }

        throw new ArgumentException($"Unsupported network type: {network?.GetType().Name}", nameof(network));
    }
}
