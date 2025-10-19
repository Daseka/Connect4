using Newtonsoft.Json;

namespace Connect4.GameParts;

[Serializable]
public class ReplayBuffer: TrainingBuffer
{
    private const string Folder = "Buffers";
    private const string FileName = "replayBuffer.json";

    public override void SaveToFile()
    {
        DirectoryInfo directoryInfo = Directory.CreateDirectory(Folder);

        string filePath = Path.Combine(directoryInfo.FullName, FileName);
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);

        File.WriteAllText(filePath, json);
    }

    public override void LoadFromFile()
    {
        ClearAll();
        DirectoryInfo directoryInfo = Directory.CreateDirectory(Folder);
        string filePath = Path.Combine(directoryInfo.FullName, FileName);

        if (!File.Exists(filePath))
        {
            return;
        }

        string json = File.ReadAllText(filePath);
        TrainingBuffer? loaded = JsonConvert.DeserializeObject<ReplayBuffer>(json);

        BoardStateHistoricalInfos = loaded?.BoardStateHistoricalInfos ?? [];
    }

}
