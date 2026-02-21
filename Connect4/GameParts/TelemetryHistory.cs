using Newtonsoft.Json;

namespace Connect4.GameParts;

[Serializable]
public class TelemetryHistory
{
    public const double Win = 1.0;
    public const double Loss = 0.0;
    public const double Draw = 0.5;
    public const int MaxBufferSize = 1000000;
    private const string Folder = "Buffers";
    private const string FileName = "TrainingData.json";

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

    public (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataRandomAveragedNewFirst(int count = 0)
    {
        var all = BoardStateHistoricalInfos.ToList();
        int minimalCount = Math.Min(count, all.Count);

        var valueStatsForBoardState = new Dictionary<string, (double redWins, double yellowWins, double draws, double weight)>();
        var sampled = new List<BoardStateHistoricInfo>(minimalCount);

        for (int i = minimalCount -1 ; i >= 0 ; i--)
        {
            BoardStateHistoricInfo info = all[i];

            if (!valueStatsForBoardState.TryGetValue(info.BoardState, out (double redWins, double yellowWins, double draws, double weight) stats))
            {
                stats = (0, 0, 0, 0);
                sampled.Add(info);
            }

            double weight = Math.Max(info.Weight, 0.01);
            stats.redWins += info.RedWins * weight;
            stats.yellowWins += info.YellowWins * weight;
            stats.draws += info.Draws * weight;
            stats.weight += weight;
            valueStatsForBoardState[info.BoardState] = stats;
        }

        var boardStateValues = new Dictionary<string, double[]>(valueStatsForBoardState.Count);
        foreach (KeyValuePair<string, (double redWins, double yellowWins, double draws, double weight)> kvp in valueStatsForBoardState)
        {
            (double redWins, double yellowWins, double draws, double weight) stats = kvp.Value;
            double totalWeight = Math.Max(stats.weight, 1e-9);
            boardStateValues[kvp.Key] =
            [
                stats.redWins / totalWeight,
                stats.yellowWins / totalWeight,
                stats.draws / totalWeight
            ];
        }

        int uniqueBoardStates = valueStatsForBoardState.Count;
        double[][] inputs = new double[uniqueBoardStates][];
        double[][] policies = new double[uniqueBoardStates][];
        double[][] values = new double[uniqueBoardStates][];

        for (int i = 0; i < sampled.Count; i++)
        {
            BoardStateHistoricInfo info = sampled[i];
            double[] boardState = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            inputs[i] = boardState;
            policies[i] = [.. info.Policy];

            (double redWins, double yellowWins, double draws, double weight) stats = valueStatsForBoardState[info.BoardState];
            double totalWeight = Math.Max(stats.weight, 1e-9);

            values[i] = boardState[^1] == 1                 
                ? [stats.redWins / totalWeight + stats.draws / totalWeight / 2]
                : [stats.yellowWins / totalWeight + stats.draws / totalWeight / 2];
        }

        return (inputs, policies, values);
    }

    public (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataSimple(int count = 0)
    {
        var all = BoardStateHistoricalInfos.ToList();
        int finalCount = Math.Min(count, all.Count);
        var chosen = new List<BoardStateHistoricInfo>(finalCount);

        double[][] inputs = new double[finalCount][];
        double[][] policies = new double[finalCount][];
        double[][] values = new double[finalCount][];

        for (int i = 0; i < finalCount; i++)
        {
            BoardStateHistoricInfo info = all[i];
            double[] boardStateArray = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            inputs[i] = boardStateArray;
            policies[i] = [.. info.Policy];
            double[] winValue = info.Draws == 1
                ? [Draw]
                : info.RedWins == 1 && boardStateArray[^1] == 1 || info.YellowWins == 1 && boardStateArray[^1] == 0 ? [Win] : [Loss];
            values[i] = winValue;
        }

        return (inputs, policies, values);
    }

    public (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataRandom(int count = 0)
    {
        var all = BoardStateHistoricalInfos.ToList();
        int finalCount = Math.Min(count, all.Count);
        var chosen = new List<BoardStateHistoricInfo>(finalCount);

        // Convert to arrays
        double[][] inputs = new double[finalCount][];
        double[][] policies = new double[finalCount][];
        double[][] values = new double[finalCount][];

        for (int i = 0; i < finalCount; i++)
        {
            BoardStateHistoricInfo info = all[Random.Shared.Next(finalCount)];
            double[] boardStateArray = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            inputs[i] = boardStateArray;
            policies[i] = [.. info.Policy];
            double[] winValue = info.Draws == 1
                ? [Draw]
                : info.RedWins == 1 && boardStateArray.Last() == 1 || info.YellowWins == 1 && boardStateArray.Last() == 0 ? [Win] : [Loss];
            values[i] = winValue;
        }

        return (inputs, policies, values);
    }

    public (double[][] input, double[][] policyOutput, double[][] valueOutput) GetTrainingDataNewFirst(int count = 0)
    {
        var all = BoardStateHistoricalInfos.ToList();
        int total = all.Count;

        int startIndexOfNew = Math.Max(0, total - NewEntries);
        int availableNew = total - startIndexOfNew;

        int desired = count <= 0 ? total : Math.Min(count, total);
        int takeNew = Math.Min(availableNew, desired);

        var chosen = new List<BoardStateHistoricInfo>(capacity: desired);

        for (int i = total - takeNew; i < total; i++)
        {
            chosen.Add(all[i]);
        }

        int remaining = desired - takeNew;

        if (remaining > 0 && startIndexOfNew > 0)
        {
            int oldPoolSize = startIndexOfNew;

            if (oldPoolSize >= remaining)
            {
                int[] indices = Enumerable.Range(0, oldPoolSize).ToArray();
                for (int i = 0; i < remaining; i++)
                {
                    int swapWith = _random.Next(i, oldPoolSize);
                    (indices[i], indices[swapWith]) = (indices[swapWith], indices[i]);
                    chosen.Add(all[indices[i]]);
                }
            }
            else
            {
                for (int i = 0; i < remaining; i++)
                {
                    int idx = _random.Next(oldPoolSize);
                    chosen.Add(all[idx]);
                }
            }
        }

        int finalCount = chosen.Count;
        double[][] inputs = new double[finalCount][];
        double[][] policies = new double[finalCount][];
        double[][] values = new double[finalCount][];

        for (int i = 0; i < finalCount; i++)
        {
            BoardStateHistoricInfo info = all[Random.Shared.Next(finalCount)];
            double[] boardStateArray = [.. BitKey.ToArray(info.BoardState).Select(x => (double)x)];
            inputs[i] = boardStateArray;
            policies[i] = [.. info.Policy];
            double[] winValue = info.Draws == 1
                ? [Draw]
                : info.RedWins == 1 && boardStateArray.Last() == 1 || info.YellowWins == 1 && boardStateArray.Last() == 0 ? [Win] : [Loss];
            values[i] = winValue;
        }

        return (inputs, policies, values);
    }

    public void LoadFromFile()
    {
        ClearAll();
        DirectoryInfo directoryInfo = Directory.CreateDirectory(Folder);
        string filePath = Path.Combine(directoryInfo.FullName, FileName);

        if (!File.Exists(filePath))
        {
            return;
        }

        string json = File.ReadAllText(filePath);
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

    public void ApplyLossPenalty(double penaltyMultiplier)
    {
        if (penaltyMultiplier <= 1)
        {
            return;
        }

        foreach (BoardStateHistoricInfo info in BoardStateHistoricalInfos)
        {
            info.Weight = Math.Max(info.Weight, 1.0) * penaltyMultiplier;
            info.IsLossSample = true;
        }
    }

    public void SaveToFile()
    {
        DirectoryInfo directoryInfo = Directory.CreateDirectory(Folder);
        string filePath = Path.Combine(directoryInfo.FullName, FileName);

        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(filePath, json);
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
                    PlayerLastPlayed = BitKey.ToArray(item.Key)[^1],
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
            _ = BoardStateHistoricalInfos.Dequeue();
        }
    }

    private void StoreInfo(BoardStateHistoricInfo info)
    {
        NewEntries++;

        BoardStateHistoricalInfos.Enqueue(info);
    }
}