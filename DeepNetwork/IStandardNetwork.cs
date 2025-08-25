namespace DeepNetwork;

public interface IStandardNetwork
{
    public static abstract string NetworkName { get; }

    public bool Trained { get; set; }
    
    public static abstract IStandardNetwork? CreateFromFile(string fileName);

    public double[] Calculate(double[] input);

    public double[] CalculateCached(string id, double[] input);

    public void SaveToFile(string fileName);

    public double ExplorationFactor { get; set; }

    public IStandardNetwork Clone();
}