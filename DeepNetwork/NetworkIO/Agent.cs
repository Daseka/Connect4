using Newtonsoft.Json;

namespace DeepNetwork.NetworkIO;

public class Agent : IDisposable
{
    public string? Created { get; set; }
    public double ExplorationFactor { get; set; } = 1.0;
    public string? FirstKill { get; set; }
    public int Generation { get; set; } = 0;
    public string? Id { get; set; }
    public int TeachingSessions { get; set; } = 0;
    public double LatestWinRate { get; set; } = 0.0;

    [JsonIgnore]
    public IStandardNetwork? PolicyNetwork { get; set; }

    public string? PolicyPath { get; set; }
    public Dictionary<string, BattleRecord> Record { get; set; } = [];

    [JsonIgnore]
    public IStandardNetwork? ValueNetwork { get; set; }

    public string? ValuePath { get; set; }

    public Agent Clone()
    {
        var clone = new Agent
        {
            Id = Guid.NewGuid().ToString()[..8],
            Created = Created,
            FirstKill = FirstKill,
            PolicyNetwork = PolicyNetwork?.Clone(),
            ValueNetwork = ValueNetwork?.Clone(),
            ExplorationFactor = ExplorationFactor,
            Generation = Generation,
            ValuePath = ValuePath,
            PolicyPath = PolicyPath,
            TeachingSessions = TeachingSessions,
            LatestWinRate = LatestWinRate,
        };

        return clone;
    }

    private bool _disposed = false;


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            PolicyNetwork?.Dispose();
        }
        catch { }

        try
        {
            ValueNetwork?.Dispose();
        }
        catch { }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}