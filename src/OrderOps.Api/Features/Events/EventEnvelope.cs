using System.Text.Json.Serialization;

namespace OrderOps.Api.Features.Events;

public sealed record EventEnvelope(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] object Data,
    [property: JsonIgnore]                string? SupplierId);
