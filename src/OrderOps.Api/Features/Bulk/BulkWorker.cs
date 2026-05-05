namespace OrderOps.Api.Features.Bulk;

public sealed class BulkWorker : BackgroundService
{
    private const int ChunkSize = 200;

    private readonly BulkQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkWorker> _log;

    public BulkWorker(BulkQueue queue, IServiceScopeFactory scopeFactory, ILogger<BulkWorker> log)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJob(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Bulk job {JobId} failed catastrophically", job.JobId);
                await TryFinalize(job.JobId, status: "failed", completed: 0, failed: job.OrderIds.Length, stoppingToken);
            }
        }
    }

    private async Task ProcessJob(BulkJob job, CancellationToken ct)
    {
        var totalCompleted = 0;
        var totalFailed = 0;

        for (var i = 0; i < job.OrderIds.Length; i += ChunkSize)
        {
            var chunk = job.OrderIds[i..Math.Min(i + ChunkSize, job.OrderIds.Length)];
            BulkChunkResult result;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IBulkRepository>();
                result = await repo.ApplyChunkAsync(job.JobId, job.Action, job.Reason, chunk, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Chunk failed for job {JobId} (offset {Offset})", job.JobId, i);
                result = new BulkChunkResult(0, chunk.Length);
            }

            totalCompleted += result.Completed;
            totalFailed    += result.Failed;

            using var stateScope = _scopeFactory.CreateScope();
            var state = stateScope.ServiceProvider.GetRequiredService<IJobStateStore>();
            await state.IncrementAsync(job.JobId, result.Completed, result.Failed, ct);
        }

        var finalStatus = (totalCompleted == 0 && totalFailed == job.OrderIds.Length) ? "failed" : "completed";
        await TryFinalize(job.JobId, finalStatus, totalCompleted, totalFailed, ct);
    }

    private async Task TryFinalize(string jobId, string status, int completed, int failed, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var state = scope.ServiceProvider.GetRequiredService<IJobStateStore>();
        var repo  = scope.ServiceProvider.GetRequiredService<IBulkRepository>();
        await state.FinalizeAsync(jobId, status, ct);
        await repo.FinalizeJobAsync(jobId, status, completed, failed, ct);
    }
}
