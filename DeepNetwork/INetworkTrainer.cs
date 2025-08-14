namespace DeepNetwork
{
    public interface INetworkTrainer
    {
        double Train(double[][] trainingInputs, double[][] trainingOutputs);
    }
}