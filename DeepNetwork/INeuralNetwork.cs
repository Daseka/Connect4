namespace DeepNetwork
{
    public interface INeuralNetwork
    {
        double Error { get; set; }
        double LastError { get; set; }

        static abstract SimpleDumbNetwork? CreateFromFile(string fileName);
        double[] Calculate(double[] input);
        double[] CalculateCached(string id, double[] input);
        void ClearCache();
        INeuralNetwork Clone();
        (double[][][] gradients, double error) ComputeGradientsAndError(double[][] trainingInputs, double[][] trainingOutputs);
        void SaveToFile(string fileName);
        void UpdateWeights();
    }
}