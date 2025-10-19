
namespace Connect4.GameParts
{
    public interface ITrainingBuffer
    {
        Queue<BoardStateHistoricInfo> BoardStateHistoricalInfos { get; set; }
        int Count { get; }
        int NewEntries { get; set; }

        void BeginAddingNewEntries();
        void ClearAll();
        (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataNewFirst(int count = 0);
        (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataRandom(int count = 0);
        (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataRandomAveragedNewFirst(int count = 0);
        void LoadFromFile();
        void MergeFrom(IEnumerable<BoardStateHistoricInfo> others);
        void MergeFrom(ITrainingBuffer other);
        void SaveToFile();
        void StoreTempData(GameBoard gameBoard, double[] policy);
        void StoreWinnerData(Winner winner);
    }
}