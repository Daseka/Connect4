namespace DeepNetwork;

public class NetworkSaver
{
    public static void SaveNetwork(IStandardNetwork? network, string filePath)
    {
        network?.SaveToFile(filePath);
    }
}
