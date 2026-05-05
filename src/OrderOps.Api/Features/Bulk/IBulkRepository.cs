namespace OrderOps.Api.Features.Bulk;

public interface IBulkRepository
{
    Task InsertJobAsync(string id, string action, int total, CancellationToken ct);
    Task<JobMirrorRow?> GetJobAsync(string id, CancellationToken ct);
    Task<BulkChunkResult> ApplyChunkAsync(string jobId, string action, string? reason, string[] ids, CancellationToken ct);
    Task FinalizeJobAsync(string id, string status, int completed, int failed, CancellationToken ct);
}
