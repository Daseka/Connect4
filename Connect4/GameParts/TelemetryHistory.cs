using Newtonsoft.Json;

namespace Connect4.GameParts;

[Serializable]
public class TelemetryHistory
{
    private const int MaxBufferSize = 500000;
    private const string TelemetryHistoryFileName = "telemetry\\telemetry_history.json";
    private readonly Queue<string> _insertionOrder = new();
    private List<int[]> _boardState = [];
    private readonly Dictionary<string, List<double[]>> _policies = [];
    private readonly Random _random = new();

    public Dictionary<string, BoardStateHistoricInfo> BoardStateHistoricalInfos { get; set; } = [];
    public int Count { get; private set; }

    public void LoadFromFile()
    {
        ClearAll();

        if (!File.Exists(TelemetryHistoryFileName))
        {
            _boardState = [];

            return;
        }

        string json = File.ReadAllText(TelemetryHistoryFileName);
        TelemetryHistory? loaded = JsonConvert.DeserializeObject<TelemetryHistory>(json);

        BoardStateHistoricalInfos = loaded?.BoardStateHistoricalInfos ?? [];

        if (loaded != null)
        {
            foreach (var key in loaded.BoardStateHistoricalInfos.Keys)
            {
                _insertionOrder.Enqueue(key);
            }
            Count = BoardStateHistoricalInfos.Count;
        }
    }

    public (double[][] input, double[][] output) GetTrainingPolicyData(int? count = null)
    {
        if (BoardStateHistoricalInfos.Count == 0)
        {
            return (Array.Empty<double[]>(), Array.Empty<double[]>());
        }

        int sampleCount = count.HasValue ? Math.Min(count.Value, BoardStateHistoricalInfos.Count) : BoardStateHistoricalInfos.Count;
        string[] keys = [.. BoardStateHistoricalInfos.Keys];

        if (sampleCount == keys.Length)
        {
            double[][] trainingData = new double[sampleCount][];
            double[][] expectedPolicies = new double[sampleCount][];
            int index = 0;

            foreach (BoardStateHistoricInfo info in BoardStateHistoricalInfos.Values)
            {
                trainingData[index] = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
                expectedPolicies[index] = info.Policy;
                index++;
            }

            return (trainingData, expectedPolicies);
        }

        List<string> randomKeys = RandomSample(keys, sampleCount);

        double[][] sampledTrainingData = new double[sampleCount][];
        double[][] sampledExpectedPolicies = new double[sampleCount][];

        for (int i = 0; i < sampleCount; i++)
        {
            BoardStateHistoricInfo info = BoardStateHistoricalInfos[randomKeys[i]];
            sampledTrainingData[i] = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            sampledExpectedPolicies[i] = info.Policy;
        }

        return (sampledTrainingData, sampledExpectedPolicies);
    }

    public (double[][] input, double[][] output) GetTrainingValueData(int? count = null)
    {
        if (BoardStateHistoricalInfos.Count == 0)
        {
            return (Array.Empty<double[]>(), Array.Empty<double[]>());
        }

        int sampleCount = count.HasValue 
            ? Math.Min(count.Value, BoardStateHistoricalInfos.Count) 
            : BoardStateHistoricalInfos.Count;

        string[] keys = [.. BoardStateHistoricalInfos.Keys];

        if (sampleCount == keys.Length)
        {
            double[][] trainingData = new double[sampleCount][];
            double[][] expectedValues = new double[sampleCount][];
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

        List<string> randomKeys = RandomSample(keys, sampleCount);

        double[][] sampledTrainingData = new double[sampleCount][];
        double[][] sampledExpectedValues = new double[sampleCount][];

        for (int i = 0; i < sampleCount; i++)
        {
            BoardStateHistoricInfo info = BoardStateHistoricalInfos[randomKeys[i]];
            sampledTrainingData[i] = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            double totalGames = info.RedWins + info.YellowWins + info.Draws;
            sampledExpectedValues[i] = sampledTrainingData[i][^1] == (double)Player.Red
                ? [info.RedWins / totalGames,]
                : [info.YellowWins / totalGames,];
        }

        return (sampledTrainingData, sampledExpectedValues);
    }

    private List<string> RandomSample(string[] population, int sampleSize)
    {
        List<string> result = [.. population];

        for (int i = 0; i < sampleSize; i++)
        {
            int j = _random.Next(i, result.Count);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return [.. result.Take(sampleSize)];
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
        _insertionOrder.Clear();
        Count = 0;
    }

    private void EnforceBufferLimit()
    {
        while (_insertionOrder.Count > MaxBufferSize)
        {
            string oldestKey = _insertionOrder.Dequeue();
            BoardStateHistoricalInfos.Remove(oldestKey);
        }
    }

    public void StoreWinnerData(Winner winner)
    {
        foreach (int[] boardState in _boardState)
        {
            string key = BitKey.ToKey(boardState);

            if (!BoardStateHistoricalInfos.TryGetValue(key, out BoardStateHistoricInfo? value))
            {
                value = new BoardStateHistoricInfo(key);
                BoardStateHistoricalInfos[key] = value;

                _insertionOrder.Enqueue(key);
                EnforceBufferLimit();
            }

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
    
    public void MergeFrom(TelemetryHistory other)
    {
        if (other == null)
        {
            return;
        }

        foreach (var entry in other.BoardStateHistoricalInfos)
        {
            if (BoardStateHistoricalInfos.TryGetValue(entry.Key, out BoardStateHistoricInfo? existingInfo))
            {
                existingInfo.RedWins += entry.Value.RedWins;
                existingInfo.YellowWins += entry.Value.YellowWins;
                existingInfo.Draws += entry.Value.Draws;
                
                if (entry.Value.Policy != null && entry.Value.Policy.Length > 0)
                {
                    if (existingInfo.Policy == null || existingInfo.Policy.Length == 0)
                    {
                        existingInfo.Policy = entry.Value.Policy;
                    }
                    else if (existingInfo.Policy.Length == entry.Value.Policy.Length)
                    {
                        for (int i = 0; i < existingInfo.Policy.Length; i++)
                        {
                            existingInfo.Policy[i] = (existingInfo.Policy[i] + entry.Value.Policy[i]) / 2;
                        }
                    }
                }
            }
            else
            {
                var newInfo = new BoardStateHistoricInfo(entry.Key)
                {
                    RedWins = entry.Value.RedWins,
                    YellowWins = entry.Value.YellowWins,
                    Draws = entry.Value.Draws,
                    Policy = entry.Value.Policy != null ? [.. entry.Value.Policy] : []
                };
                
                BoardStateHistoricalInfos[entry.Key] = newInfo;
                _insertionOrder.Enqueue(entry.Key);
                Count++;
                
                EnforceBufferLimit();
            }
        }
        
        foreach (var state in other._boardState)
        {
            _boardState.Add([.. state]);
        }
        
        foreach (var policy in other._policies)
        {
            if (_policies.TryGetValue(policy.Key, out List<double[]>? existingPolicy))
            {
                foreach (var p in policy.Value)
                {
                    existingPolicy.Add([.. p]);
                }
            }
            else
            {
                var newPolicyList = new List<double[]>();
                foreach (var p in policy.Value)
                {
                    newPolicyList.Add([.. p]);
                }
                _policies[policy.Key] = newPolicyList;
            }
        }
    }
}