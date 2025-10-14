using Newtonsoft.Json;

namespace Connect4.GameParts;

public class ReplayBuffer: TrainingBuffer
{
    private const string BufferFolder = "Buffers";
    private const string BufferFileName = "replayBuffer.json";

    public override void SaveToFile()
    {
        DirectoryInfo directoryInfo = Directory.CreateDirectory(BufferFolder);

        string filePath = Path.Combine(directoryInfo.FullName, BufferFileName);
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);

        File.WriteAllText(filePath, json);
    }

    public override void LoadFromFile()
    {
        ClearAll();

        if (!File.Exists(BufferFileName))
        {
            return;
        }

        string json = File.ReadAllText(BufferFileName);
        TrainingBuffer? loaded = JsonConvert.DeserializeObject<TrainingBuffer>(json);

        BoardStateHistoricalInfos = loaded?.BoardStateHistoricalInfos ?? [];
    }

}
