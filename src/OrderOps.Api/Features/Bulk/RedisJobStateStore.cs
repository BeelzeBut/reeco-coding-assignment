using StackExchange.Redis;

namespace OrderOps.Api.Features.Bulk;

public sealed class RedisJobStateStore : IJobStateStore
{
    private readonly IConnectionMultiplexer _mux;

    public RedisJobStateStore(IConnectionMultiplexer mux) => _mux = mux;

    private static RedisKey Key(string id) => $"job:{id}";

    public async Task InitAsync(string id, int total, CancellationToken ct)
    {
        var db = _mux.GetDatabase();
        await db.HashSetAsync(Key(id), new HashEntry[]
        {
            new("status", "processing"),
            new("total", total),
            new("completed", 0),
            new("failed", 0),
        });
    }

    public async Task IncrementAsync(string id, int completedDelta, int failedDelta, CancellationToken ct)
    {
        var db = _mux.GetDatabase();
        if (completedDelta != 0) await db.HashIncrementAsync(Key(id), "completed", completedDelta);
        if (failedDelta != 0)    await db.HashIncrementAsync(Key(id), "failed", failedDelta);
    }

    public async Task FinalizeAsync(string id, string status, CancellationToken ct)
    {
        var db = _mux.GetDatabase();
        await db.HashSetAsync(Key(id), "status", status);
        await db.KeyExpireAsync(Key(id), TimeSpan.FromDays(1));
    }

    public async Task<JobStateSnapshot?> GetAsync(string id, CancellationToken ct)
    {
        var db = _mux.GetDatabase();
        var entries = await db.HashGetAllAsync(Key(id));
        if (entries.Length == 0) return null;

        string status = "";
        int total = 0, completed = 0, failed = 0;
        foreach (var e in entries)
        {
            switch ((string)e.Name!)
            {
                case "status":    status = e.Value!; break;
                case "total":     total = (int)e.Value; break;
                case "completed": completed = (int)e.Value; break;
                case "failed":    failed = (int)e.Value; break;
            }
        }
        return new JobStateSnapshot(status, total, completed, failed);
    }
}
