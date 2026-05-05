namespace OrderOps.Api.Features.Bulk;

public interface IJobStateStore
{
    Task InitAsync(string id, int total, CancellationToken ct);
    Task IncrementAsync(string id, int completedDelta, int failedDelta, CancellationToken ct);
    Task FinalizeAsync(string id, string status, CancellationToken ct);
    Task<JobStateSnapshot?> GetAsync(string id, CancellationToken ct);
}
