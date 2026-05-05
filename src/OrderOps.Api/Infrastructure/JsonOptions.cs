using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderOps.Api.Infrastructure;

public static class JsonOptionsConfig
{
    public static void Apply(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.PropertyNameCaseInsensitive = true;
        o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }
}
