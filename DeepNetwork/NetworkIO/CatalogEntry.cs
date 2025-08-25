namespace DeepNetwork.NetworkIO;

public class CatalogEntry
{
    public string? AgentId { get; set; }
    public string? ValuePath { get; set; }
    public string? PolicyPath { get; set; }
    public string? FirstKill { get; set; }
    public Dictionary<string,BattleRecord>? Record { get; set; }
}
