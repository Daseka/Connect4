using Newtonsoft.Json;

namespace DeepNetwork.NetworkIO;

public class AgentCatalog(int catalogSize)
{
    private const string CatalogFile = "catalog.json";
    private const string CatalogFolder = "AgentCatalog";
    private readonly int _catalogSize = catalogSize;
    private readonly Queue<string> _agentIds = new();

    public Dictionary<string, Agent> Entries { get; set; } = [];

    public void Add(Agent agent)
    {
        MaintainCatalogSize(Entries, _agentIds, _catalogSize);

        _ = agent?.Id ?? throw new ArgumentNullException(nameof(agent));

        DirectoryInfo directoryInfo = new DirectoryInfo(CatalogFolder); 

        agent.ValuePath = Path.Combine(directoryInfo.FullName, $"{agent.Id}\\{agent.Id}_values.json");
        agent.PolicyPath = Path.Combine(directoryInfo.FullName, $"{agent.Id}\\{agent.Id}_policy.json");

        Entries[agent.Id] = agent;
        _agentIds.Enqueue(agent.Id);
    }

    public Agent? GetLatestAgent()
    {
        Agent? oldestAgent = null;
        foreach (Agent agent in Entries.Values)
        {
            if (agent.Generation > (oldestAgent?.Generation ?? 0))
            {
                oldestAgent = agent;
            }
        }

        return oldestAgent;
    }

    public void LoadCatalog()
    {
        Dictionary<string, Agent>? enteries = null;
        var file = new FileInfo(Path.Combine(new DirectoryInfo(CatalogFolder).FullName, CatalogFile));

        if (file.Exists)
        {
            string jsonContent = File.ReadAllText(file.FullName);
            enteries = JsonConvert.DeserializeObject<Dictionary<string, Agent>>(jsonContent);
        }

        Entries = [];
        if (enteries is null)
        {
            return;
        }

        foreach (Agent agent in enteries.Values)
        {
            agent.ValueNetwork = agent.ValuePath is null
                ? null
                : NetworkLoader.LoadNetwork(agent.ValuePath);

            agent.PolicyNetwork = agent.PolicyPath is null
                ? null
                : NetworkLoader.LoadNetwork(agent.PolicyPath);

            Add(agent);
        }
    }

    public void SaveCatalog()
    {
        DirectoryInfo directoryInfo = Directory.CreateDirectory(CatalogFolder);

        foreach (Agent agent in Entries.Values)
        {
            _ = agent.ValuePath ?? throw new InvalidOperationException($"Agent {agent.Id} does not have a value path.");
            _ = agent.PolicyPath ?? throw new InvalidOperationException($"Agent {agent.Id} does not have a policy path.");

            Directory.CreateDirectory(Path.GetDirectoryName(agent.ValuePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(agent.PolicyPath)!);

            agent.ValueNetwork?.SaveToFile(agent.ValuePath);
            agent.PolicyNetwork?.SaveToFile(agent.PolicyPath);
        }

        string catalogFilePath = Path.Combine(directoryInfo.FullName, CatalogFile);
        string catalogJson = JsonConvert.SerializeObject(Entries, Formatting.Indented);

        File.WriteAllText(catalogFilePath, catalogJson);
    }

    private static void MaintainCatalogSize(Dictionary<string, Agent> entries, Queue<string> agentIds, int catalogSize)
    {
        if (agentIds.Count >= catalogSize)
        {
            agentIds.TryDequeue(out string? oldestAgentId);
            if (oldestAgentId is not null)
            {
                entries.Remove(oldestAgentId);
            }
        }
    }
}