using Newtonsoft.Json;

namespace Connect4.GameParts;

public class BitKeyConverter : JsonConverter<BitKey>
{
    public override void WriteJson(JsonWriter writer, BitKey value, JsonSerializer serializer)
    {
        writer.WriteValue($"{value.High},{value.Low}");
    }

    public override BitKey ReadJson(JsonReader reader, Type objectType, BitKey existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var parts = reader.Value.ToString().Split(',');
        return new BitKey
        {
            High = ulong.Parse(parts[0]),
            Low = ulong.Parse(parts[1])
        };
    }
}
