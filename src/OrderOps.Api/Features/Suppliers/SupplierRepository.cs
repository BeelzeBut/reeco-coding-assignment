using Dapper;
using Npgsql;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Suppliers;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public SupplierRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<PagedResult<SupplierListItem>> ListAsync(int limit, int offset, CancellationToken ct)
    {
        const string sql = """
            SELECT id, name, email, rating, country, active, created_at
            FROM   suppliers
            ORDER  BY id
            LIMIT  @limit OFFSET @offset;

            SELECT count(*) FROM suppliers;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { limit, offset }, cancellationToken: ct));

        var rows = (await grid.ReadAsync<SupplierListItem>()).AsList();
        var total = await grid.ReadSingleAsync<long>();
        return new PagedResult<SupplierListItem>(rows, total, limit, offset);
    }

    public async Task<SupplierDetail?> GetByIdAsync(string id, CancellationToken ct)
    {
        const string sql = """
            SELECT s.id, s.name, s.email, s.rating, s.country, s.active, s.created_at,
                   COALESCE((SELECT count(*)            FROM orders o WHERE o.supplier_id = s.id), 0) AS order_count,
                   COALESCE((SELECT sum(o.total_price)  FROM orders o WHERE o.supplier_id = s.id), 0) AS total_revenue
            FROM   suppliers s
            WHERE  s.id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SupplierDetail>(new CommandDefinition(
            sql, new { id }, cancellationToken: ct));
    }
}
