using Newtonsoft.Json;

namespace Connect4.GameParts;

[Serializable]
public class TelemetryHistory
{
    public  int Count { get; private set; }
    private const string TelemetryHistoryFileName = "telemetry_history.json";
    private List<int[]> _boardState = [];
    private readonly Dictionary<string, List<double[]>> _policies = [];

    public Dictionary<string, BoardStateHistoricInfo> BoardStateHistoricalInfos { get; set; } = [];

    public void LoadFromFile()
    {
        if (!File.Exists(TelemetryHistoryFileName))
        {
            _boardState = [];

            return;
        }

        string json = File.ReadAllText(TelemetryHistoryFileName);
        TelemetryHistory? loaded = JsonConvert.DeserializeObject<TelemetryHistory>(json);

        BoardStateHistoricalInfos = loaded?.BoardStateHistoricalInfos ?? [];
    }

    public (double[][] input, double[][] output) GetTrainingPolicyData()
    {
        double[][] trainingData = new double[BoardStateHistoricalInfos.Count][];
        double[][] expectedPolicies = new double[BoardStateHistoricalInfos.Count][];
        int index = 0;

        foreach (BoardStateHistoricInfo info in BoardStateHistoricalInfos.Values)
        {
            trainingData[index] = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            expectedPolicies[index] = info.Policy;
            index++;
        }

        return (trainingData, expectedPolicies);
    }

    public (double[][] input, double[][] output) GetTrainingValueData()
    {
        double[][] trainingData = new double[BoardStateHistoricalInfos.Count][];
        double[][] expectedValues = new double[BoardStateHistoricalInfos.Count][];
        int index = 0;

        foreach (BoardStateHistoricInfo info in BoardStateHistoricalInfos.Values)
        {
            trainingData[index] = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            double totalGames = info.RedWins + info.YellowWins + info.Draws;
            expectedValues[index] = trainingData[index][^1] == (double)Player.Red
                ? [info.RedWins / totalGames,]
                : [info.YellowWins / totalGames,];
            index++;
        }

        return (trainingData, expectedValues);
    }

    public void SaveToFile()
    {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(TelemetryHistoryFileName, json);
    }

    public void StoreTempData(GameBoard gameBoard, double[] policy)
    {
        int[] state = gameBoard.StateToArray();

        if (_policies.TryGetValue(BitKey.ToKey(state), out List<double[]>? existingPolicy))
        {
            existingPolicy.Add(policy);

        }
        else
        {
            _policies[BitKey.ToKey(state)] = [policy];
        }

        _boardState.Add(state);
        Count++;
    }

    public void ClearAll()
    {
        _boardState.Clear();
        _policies.Clear();
        BoardStateHistoricalInfos.Clear();
        Count = 0;
    }

    public void StoreWinnerData(Winner winner)
    {
        foreach (int[] boardState in _boardState)
        {

            string key = BitKey.ToKey(boardState);

            // add the board state 
            if (!BoardStateHistoricalInfos.TryGetValue(key, out BoardStateHistoricInfo? value))
            {
                value = new BoardStateHistoricInfo(key);
                BoardStateHistoricalInfos[key] = value;
            }

            // add the averages of the policies
            if (_policies.TryGetValue(key, out List<double[]>? policies))
            {
                double[] averagePolicy = new double[policies[0].Length];
                foreach (double[] policy in policies)
                {
                    for (int i = 0; i < policy.Length; i++)
                    {
                        averagePolicy[i] += policy[i];
                    }
                }

                for (int i = 0; i < averagePolicy.Length; i++)
                {
                    averagePolicy[i] /= policies.Count;
                }

                value.Policy = averagePolicy;
            }

            // add the winnder data
            if (winner == Winner.Red)
            {
                value.RedWins++;
            }
            else if (winner == Winner.Yellow)
            {
                value.YellowWins++;
            }
            else if (winner == Winner.Draw)
            {
                value.Draws++;
            }
        }

        _boardState.Clear();
        _policies.Clear();
    }
}