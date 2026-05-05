using Dapper;
using Npgsql;

namespace OrderOps.Api.Features.Bulk;

public sealed class BulkRepository : IBulkRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public BulkRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task InsertJobAsync(string id, string action, int total, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO jobs (id, status, total, completed, failed, action)
            VALUES (@id, 'processing', @total, 0, 0, @action);
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { id, total, action }, cancellationToken: ct));
    }

    public async Task<JobMirrorRow?> GetJobAsync(string id, CancellationToken ct)
    {
        const string sql = "SELECT id, status, total, completed, failed, action FROM jobs WHERE id = @id;";
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<JobMirrorRow>(
            new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task FinalizeJobAsync(string id, string status, int completed, int failed, CancellationToken ct)
    {
        const string sql = """
            UPDATE jobs
            SET    status = @status, completed = @completed, failed = @failed, finished_at = now()
            WHERE  id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            sql, new { id, status, completed, failed }, cancellationToken: ct));
    }

    public async Task<BulkChunkResult> ApplyChunkAsync(
        string jobId, string action, string? reason, string[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return new BulkChunkResult(0, 0);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        var existing = (await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT id FROM orders WHERE id = ANY(@ids);",
            new { ids }, cancellationToken: ct))).ToHashSet(StringComparer.Ordinal);

        var nonexistent = ids.Length - existing.Count;

        await using var tx = await conn.BeginTransactionAsync(ct);

        var locked = (await conn.QueryAsync<(string id, string status, int version)>(new CommandDefinition(
            "SELECT id, status, version FROM orders WHERE id = ANY(@ids) FOR UPDATE SKIP LOCKED;",
            new { ids }, transaction: tx, cancellationToken: ct))).ToList();

        var lockedIds = locked.Select(r => r.id).ToHashSet(StringComparer.Ordinal);
        var overlapCount = existing.Count(id => !lockedIds.Contains(id));

        var completed = overlapCount;
        var failed = nonexistent;

        if (action == BulkActions.Flag)
        {
            const string insertFlag = """
                INSERT INTO order_flags (order_id, source_job_id, reason)
                VALUES (@id, @jobId, @reason)
                ON CONFLICT (order_id) DO NOTHING;
                """;

            foreach (var row in locked)
            {
                if (row.status == "cancelled") { failed++; continue; }
                await conn.ExecuteAsync(new CommandDefinition(
                    insertFlag, new { id = row.id, jobId, reason }, transaction: tx, cancellationToken: ct));
                completed++;
            }
        }
        else
        {
            var newStatus = BulkActions.MapToStatus(action)
                ?? throw new InvalidOperationException($"unsupported action '{action}'");

            const string updateStatus = """
                UPDATE orders
                SET    status = @newStatus, updated_at = now(), version = version + 1
                WHERE  id = @id AND version = @v;
                """;

            foreach (var row in locked)
            {
                if (row.status == "cancelled") { failed++; continue; }
                var rows = await conn.ExecuteAsync(new CommandDefinition(
                    updateStatus, new { id = row.id, newStatus, v = row.version },
                    transaction: tx, cancellationToken: ct));
                if (rows == 1) completed++;
                else failed++;
            }
        }

        await tx.CommitAsync(ct);
        return new BulkChunkResult(completed, failed);
    }
}
