using Newtonsoft.Json;

namespace Connect4.GameParts;

[Serializable]
public class TelemetryHistory
{
    public const int MaxBufferSize = 300000;
    private const string TelemetryHistoryFileName = "telemetry\\telemetry_history.json";

    private readonly Dictionary<string, List<double[]>> _policies = [];
    private readonly Random _random = new();

    public Queue<BoardStateHistoricInfo> BoardStateHistoricalInfos { get; set; } = [];
    public int Count => BoardStateHistoricalInfos.Count;
    public int NewEntries { get; set; } = 0;

    public void BeginAddingNewEntries()
    {
        NewEntries = 0;
    }

    public void ClearAll()
    {
        _policies.Clear();
        BoardStateHistoricalInfos.Clear();
    }
    
    public (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataRandom(int count = 0)
    {
        var all = BoardStateHistoricalInfos.ToList();
        int finalCount = Math.Min(count, all.Count);
        var chosen = new List<BoardStateHistoricInfo>(finalCount);

        // Convert to arrays
        var inputs = new double[finalCount][];
        var policies = new double[finalCount][];
        var values = new double[finalCount][];

        for (int i = 0; i < finalCount; i++)
        {
            var info = all[Random.Shared.Next(finalCount)];
            inputs[i] = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            policies[i] = [.. info.Policy];
            values[i] = [info.RedWins, info.YellowWins, info.Draws];
        }

        return (inputs, policies, values);
    }

    public (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingData(int count = 0)
    {
        var all = BoardStateHistoricalInfos.ToList();
        int total = all.Count;

        // Index where new entries begin
        int startIndexOfNew = Math.Max(0, total - NewEntries);
        int availableNew = total - startIndexOfNew;

        // Desired total samples
        int desired = count <= 0 ? total : Math.Min(count, total);

        // Take as many new entries as will fit
        int takeNew = Math.Min(availableNew, desired);

        var chosen = new List<BoardStateHistoricInfo>(capacity: desired);

        // Always include the most recent (takeNew) entries (preserve recency order)
        for (int i = total - takeNew; i < total; i++)
        {
            chosen.Add(all[i]);
        }

        int remaining = desired - takeNew;

        // If we still need more, sample from the older pool [0, startIndexOfNew)
        if (remaining > 0 && startIndexOfNew > 0)
        {
            int oldPoolSize = startIndexOfNew;

            if (oldPoolSize >= remaining)
            {
                // Sample without replacement
                // Simple Fisher-Yates style partial shuffle
                var indices = Enumerable.Range(0, oldPoolSize).ToArray();
                for (int i = 0; i < remaining; i++)
                {
                    int swapWith = _random.Next(i, oldPoolSize);
                    (indices[i], indices[swapWith]) = (indices[swapWith], indices[i]);
                    chosen.Add(all[indices[i]]);
                }
            }
            else
            {
                // Not enough distinct old entries, sample with replacement
                for (int i = 0; i < remaining; i++)
                {
                    int idx = _random.Next(oldPoolSize);
                    chosen.Add(all[idx]);
                }
            }
        }

        // Convert to arrays
        int finalCount = chosen.Count;
        var inputs = new double[finalCount][];
        var policies = new double[finalCount][];
        var values = new double[finalCount][];

        for (int i = 0; i < finalCount; i++)
        {
            var info = chosen[i];
            inputs[i] = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            policies[i] = [.. info.Policy];
            values[i] = [info.RedWins, info.YellowWins, info.Draws];
        }

        return (inputs, policies, values);
    }

    public void LoadFromFile()
    {
        ClearAll();

        if (!File.Exists(TelemetryHistoryFileName))
        {
            return;
        }

        string json = File.ReadAllText(TelemetryHistoryFileName);
        TelemetryHistory? loaded = JsonConvert.DeserializeObject<TelemetryHistory>(json);

        BoardStateHistoricalInfos = loaded?.BoardStateHistoricalInfos ?? [];
    }

    public void MergeFrom(TelemetryHistory other)
    {
        if (other == null)
        {
            return;
        }

        foreach (BoardStateHistoricInfo info in other.BoardStateHistoricalInfos)
        {
            StoreInfo(info);
        }

        EnforceBufferLimit();
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
    }

    public void StoreWinnerData(Winner winner)
    {
        foreach (KeyValuePair<string, List<double[]>> item in _policies)
        {
            foreach (double[] policyValues in item.Value)
            {
                var info = new BoardStateHistoricInfo(item.Key)
                {
                    Policy = [.. policyValues]
                };

                if (winner == Winner.Red)
                {
                    info.RedWins = 1;
                }
                else if (winner == Winner.Yellow)
                {
                    info.YellowWins = 1;
                }
                else if (winner == Winner.Draw)
                {
                    info.Draws = 1;
                }

                StoreInfo(info);
            }
        }

        EnforceBufferLimit();

        _policies.Clear();
    }

    private void EnforceBufferLimit()
    {
        while (BoardStateHistoricalInfos.Count > MaxBufferSize)
        {
            BoardStateHistoricalInfos.Dequeue();
        }
    }

    private void StoreInfo(BoardStateHistoricInfo info)
    {
        NewEntries++;

        BoardStateHistoricalInfos.Enqueue(info);
    }
}