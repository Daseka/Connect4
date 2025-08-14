using MathNet.Numerics.LinearAlgebra.Double;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DeepNetwork;

public class DenseMatrixConverter : JsonConverter<DenseMatrix>
{
    public override DenseMatrix? ReadJson(
        JsonReader reader, 
        Type objectType, 
        DenseMatrix? existingValue, 
        bool hasExistingValue, 
        JsonSerializer serializer)
    {
        var jArray = JArray.Load(reader);
        var rows = jArray.Count;
        var cols = jArray[0].Count();
        var matrix = new double[rows, cols];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = (double)jArray[i][j];
            }
        }

        return DenseMatrix.OfArray(matrix);
    }

    public override void WriteJson(JsonWriter writer, DenseMatrix? value, JsonSerializer serializer)
    {
        var array = value?.ToArray();
        serializer.Serialize(writer, array);
    }
}
