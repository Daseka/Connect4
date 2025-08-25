namespace DeepNetwork.NetworkIO;

public class NetworkLoader
{
    public static IStandardNetwork? LoadNetwork(string filePath)
    {
        IStandardNetwork? flatDumbNetwork;
        IStandardNetwork? miniBatchMatrixNetwork;

        flatDumbNetwork = FlatDumbNetwork.CreateFromFile(filePath);
        miniBatchMatrixNetwork = MiniBatchMatrixNetwork.CreateFromFile(filePath);

        return flatDumbNetwork ?? miniBatchMatrixNetwork;
    }
}
