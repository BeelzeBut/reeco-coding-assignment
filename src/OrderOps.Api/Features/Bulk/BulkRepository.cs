using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using OrderOps.Api.Data;
using OrderOps.Api.Data.Entities;

namespace OrderOps.Api.Features.Bulk;

public sealed class BulkRepository : IBulkRepository
{
    private readonly AppDbContext _db;

    public BulkRepository(AppDbContext db) => _db = db;

    public async Task InsertJobAsync(string id, string action, int total, CancellationToken ct)
    {
        _db.Jobs.Add(new Job
        {
            Id = id,
            Status = "processing",
            Total = total,
            Completed = 0,
            Failed = 0,
            Action = action,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<JobMirrorRow?> GetJobAsync(string id, CancellationToken ct)
    {
        var job = await _db.Jobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null) return null;

        return new JobMirrorRow
        {
            Id = job.Id,
            Status = job.Status,
            Total = job.Total,
            Completed = job.Completed,
            Failed = job.Failed,
            Action = job.Action,
        };
    }

    public async Task FinalizeJobAsync(string id, string status, int completed, int failed, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null) return;
        job.Status = status;
        job.Completed = completed;
        job.Failed = failed;
        job.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private sealed class LockedRow
    {
        public string id { get; set; } = "";
        public string status { get; set; } = "";
        public int version { get; set; }
    }

    public async Task<BulkChunkResult> ApplyChunkAsync(
        string jobId, string action, string? reason, string[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return new BulkChunkResult(0, 0);

        var existing = await _db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .Select(o => o.Id)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        var nonexistent = ids.Length - existingSet.Count;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var idsParam = new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = ids };
        var locked = await _db.Database
            .SqlQueryRaw<LockedRow>(
                "SELECT id, status, version FROM orders WHERE id = ANY({0}) FOR UPDATE SKIP LOCKED",
                idsParam)
            .ToListAsync(ct);

        var lockedIds = locked.Select(r => r.id).ToHashSet(StringComparer.Ordinal);
        var overlapCount = existingSet.Count(id => !lockedIds.Contains(id));

        var completed = overlapCount;
        var failed = nonexistent;

        if (action == BulkActions.Flag)
        {
            const string insertFlag =
                "INSERT INTO order_flags (order_id, source_job_id, reason) " +
                "VALUES (@order_id, @source_job_id, @reason) " +
                "ON CONFLICT (order_id) DO NOTHING";

            foreach (var row in locked)
            {
                if (row.status == "cancelled") { failed++; continue; }
                await _db.Database.ExecuteSqlRawAsync(insertFlag, new[]
                {
                    new NpgsqlParameter("order_id", NpgsqlDbType.Varchar) { Value = row.id },
                    new NpgsqlParameter("source_job_id", NpgsqlDbType.Varchar) { Value = jobId },
                    new NpgsqlParameter("reason", NpgsqlDbType.Text) { Value = (object?)reason ?? DBNull.Value },
                }, ct);
                completed++;
            }
        }
        else
        {
            var newStatus = BulkActions.MapToStatus(action)
                ?? throw new InvalidOperationException($"unsupported action '{action}'");

            const string updateSql =
                "UPDATE orders " +
                "SET    status = @status, updated_at = now(), version = version + 1 " +
                "WHERE  id = @id AND version = @v";

            foreach (var row in locked)
            {
                if (row.status == "cancelled") { failed++; continue; }
                var rows = await _db.Database.ExecuteSqlRawAsync(updateSql, new[]
                {
                    new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = newStatus },
                    new NpgsqlParameter("id", NpgsqlDbType.Varchar) { Value = row.id },
                    new NpgsqlParameter("v", NpgsqlDbType.Integer) { Value = row.version },
                }, ct);
                if (rows == 1) completed++;
                else failed++;
            }
        }

        await tx.CommitAsync(ct);
        return new BulkChunkResult(completed, failed);
    }
}
