using Dapper;
using Npgsql;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Orders;

public sealed class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public OrderRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    private const string DetailColumns = """
        o.id, o.supplier_id, o.product_id, o.quantity, o.unit_price, o.total_price,
        o.status, o.priority, o.created_at, o.updated_at, o.warehouse, o.notes,
        s.name AS supplier_name, p.name AS product_name
        """;

    public async Task<PagedResult<OrderListItem>> ListAsync(int limit, int offset, CancellationToken ct)
    {
        const string sql = """
            SELECT id, supplier_id, product_id, quantity, unit_price, total_price,
                   status, priority, created_at, updated_at, warehouse, notes
            FROM   orders
            ORDER  BY id
            LIMIT  @limit OFFSET @offset;

            SELECT count(*) FROM orders;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { limit, offset }, cancellationToken: ct));

        var rows = (await grid.ReadAsync<OrderListItem>()).AsList();
        var total = await grid.ReadSingleAsync<long>();
        return new PagedResult<OrderListItem>(rows, total, limit, offset);
    }

    public async Task<OrderDetail?> GetByIdAsync(string id, CancellationToken ct)
    {
        var sql = $"""
            SELECT {DetailColumns}
            FROM   orders o
            JOIN   suppliers s ON s.id = o.supplier_id
            JOIN   products  p ON p.id = o.product_id
            WHERE  o.id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<OrderDetail>(new CommandDefinition(
            sql, new { id }, cancellationToken: ct));
    }

    public async Task<UpdateStatusOutcome> UpdateStatusAsync(string id, string newStatus, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        var current = await conn.QuerySingleOrDefaultAsync<(string status, int version)?>(
            new CommandDefinition("SELECT status, version FROM orders WHERE id = @id",
                new { id }, cancellationToken: ct));

        if (current is null) return new UpdateStatusOutcome.NotFound();
        if (current.Value.status == OrderStatuses.Cancelled) return new UpdateStatusOutcome.AlreadyCancelled();

        const string updateSql = """
            UPDATE orders
            SET    status = @newStatus, updated_at = now(), version = version + 1
            WHERE  id = @id AND version = @v
            RETURNING 1;
            """;

        var rows = await conn.ExecuteAsync(new CommandDefinition(
            updateSql, new { id, newStatus, v = current.Value.version }, cancellationToken: ct));

        if (rows == 0) return new UpdateStatusOutcome.VersionConflict();

        var detailSql = $"""
            SELECT {DetailColumns}
            FROM   orders o
            JOIN   suppliers s ON s.id = o.supplier_id
            JOIN   products  p ON p.id = o.product_id
            WHERE  o.id = @id;
            """;

        var detail = await conn.QuerySingleAsync<OrderDetail>(new CommandDefinition(
            detailSql, new { id }, cancellationToken: ct));
        return new UpdateStatusOutcome.Updated(detail);
    }
}
