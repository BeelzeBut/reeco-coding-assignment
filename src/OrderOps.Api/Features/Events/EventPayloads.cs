using System.Text.Json.Serialization;

namespace OrderOps.Api.Features.Events;

public sealed record OrderUpdatedPayload(
    string Id,
    string OldStatus,
    string NewStatus,
    DateTime UpdatedAt);

public sealed record BulkCompletedPayload(
    [property: JsonPropertyName("jobId")] string JobId);
