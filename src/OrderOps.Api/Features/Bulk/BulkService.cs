using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Bulk;

public sealed class BulkService
{
    private const int MaxBatchSize = 10_000;
    private const int MaxReasonLength = 4096;

    private readonly IBulkRepository _repo;
    private readonly IJobStateStore _state;
    private readonly BulkQueue _queue;

    public BulkService(IBulkRepository repo, IJobStateStore state, BulkQueue queue)
    {
        _repo = repo;
        _state = state;
        _queue = queue;
    }

    public async Task<BulkActionResponse> EnqueueAsync(BulkActionRequest req, CancellationToken ct)
    {
        if (req.OrderIds is null || req.OrderIds.Length == 0)
            throw new ValidationException("orderIds is required", "validation");
        if (req.OrderIds.Length > MaxBatchSize)
            throw new ValidationException($"batch size exceeds {MaxBatchSize}", "validation");
        if (string.IsNullOrWhiteSpace(req.Action))
            throw new ValidationException("action is required", "validation");
        if (!BulkActions.IsValid(req.Action))
            throw new ValidationException($"invalid action '{req.Action}'", "validation");
        if (req.Reason is { Length: > MaxReasonLength })
            throw new ValidationException($"reason exceeds {MaxReasonLength} characters", "reason_too_long");

        var jobId = "job_" + Guid.NewGuid().ToString("n")[..24];

        await _repo.InsertJobAsync(jobId, req.Action, req.OrderIds.Length, ct);
        await _state.InitAsync(jobId, req.OrderIds.Length, ct);

        await _queue.Writer.WriteAsync(new BulkJob(jobId, req.Action, req.Reason, req.OrderIds), ct);

        return new BulkActionResponse(jobId);
    }

    public async Task<JobStatusResponse> GetJobAsync(string id, CancellationToken ct)
    {
        var snapshot = await _state.GetAsync(id, ct);
        if (snapshot is not null)
            return new JobStatusResponse(snapshot.Status,
                new JobProgress(snapshot.Total, snapshot.Completed, snapshot.Failed));

        var mirror = await _repo.GetJobAsync(id, ct);
        if (mirror is null) throw new NotFoundException("Job");

        return new JobStatusResponse(mirror.Status,
            new JobProgress(mirror.Total, mirror.Completed, mirror.Failed));
    }
}
