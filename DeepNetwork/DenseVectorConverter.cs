using MathNet.Numerics.LinearAlgebra.Double;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeepNetwork;

public class DenseVectorConverter : JsonConverter<DenseVector>
{
    public override DenseVector? ReadJson(
        JsonReader reader, 
        Type objectType, 
        DenseVector? existingValue, 
        bool hasExistingValue, 
        JsonSerializer serializer)
    {
        var jArray = JArray.Load(reader);
        var array = jArray.ToObject<double[]>();
        return DenseVector.OfArray(array!);
    }

    public override void WriteJson(JsonWriter writer, DenseVector? value, JsonSerializer serializer)
    {
        var array = value?.ToArray();
        serializer.Serialize(writer, array);
    }
}
