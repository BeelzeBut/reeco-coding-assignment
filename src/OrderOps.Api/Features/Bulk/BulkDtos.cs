using System.Text.Json.Serialization;

namespace OrderOps.Api.Features.Bulk;

public sealed record BulkActionRequest(
    [property: JsonPropertyName("orderIds")] string[]? OrderIds,
    [property: JsonPropertyName("action")]   string? Action,
    [property: JsonPropertyName("reason")]   string? Reason);

public sealed record BulkActionResponse(
    [property: JsonPropertyName("jobId")] string JobId);

public sealed record BulkActionsRequest(string[]? OrderIds, string? Action, string? Reason);

public sealed record BulkActionsResponse(string JobId);

public sealed record JobProgress(int Total, int Completed, int Failed);

public sealed record JobStatusResponse(string Status, JobProgress Progress);

public sealed record BulkJob(string JobId, string Action, string? Reason, string[] OrderIds);

public sealed record BulkChunkResult(int Completed, int Failed);

public sealed class JobMirrorRow
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public string Action { get; set; } = "";
}

public sealed record JobStateSnapshot(string Status, int Total, int Completed, int Failed);
