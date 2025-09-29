using Newtonsoft.Json;

namespace DeepNetwork.NetworkIO;

public class Agent
{
    public string? Created { get; set; }
    public string? FirstKill { get; set; }
    public string? Id { get; set; }
    public string? PolicyPath { get; set; }
    public string? ValuePath { get; set; }
    public int Generation { get; set; } = 0;
    public double ExplorationFactor { get; set; } = 1.0;
    public Dictionary<string, BattleRecord> Record { get; set; } = [];

    [JsonIgnore]
    public IStandardNetwork? ValueNetwork { get; set; }
    [JsonIgnore]
    public IStandardNetwork? PolicyNetwork { get; set; }
}
