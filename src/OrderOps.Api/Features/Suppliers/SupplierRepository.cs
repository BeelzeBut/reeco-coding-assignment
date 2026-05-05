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

    public async Task<SupplierPerformance?> GetPerformanceAsync(string id, CancellationToken ct)
    {
        const string sql = """
            SELECT count(*)                                                  AS exists_marker
            FROM   suppliers WHERE id = @id;

            SELECT count(*)                                                  AS total_orders,
                   COALESCE(avg(extract(epoch FROM updated_at - created_at)
                                / 86400.0) FILTER (WHERE status = 'delivered'), 0)
                                                                             AS avg_delivery_days,
                   CASE WHEN count(*) = 0 THEN 0
                        ELSE count(*) FILTER (WHERE status = 'rejected')::float8
                             / count(*)::float8
                   END                                                       AS rejection_rate,
                   COALESCE(avg(total_price), 0)                             AS avg_order_value
            FROM   orders
            WHERE  supplier_id = @id;

            WITH bounds AS (
                SELECT date_trunc('month', min(created_at)) AS lo,
                       date_trunc('month', max(created_at)) AS hi
                FROM   orders
            ),
            months AS (
                SELECT to_char(generate_series(lo, hi, interval '1 month'), 'YYYY-MM') AS month
                FROM   bounds
            )
            SELECT m.month                                                   AS month,
                   COUNT(o.id)                                               AS order_count,
                   COALESCE(sum(o.total_price), 0)                           AS revenue
            FROM   months m
            LEFT JOIN orders o
                   ON to_char(date_trunc('month', o.created_at), 'YYYY-MM') = m.month
                  AND o.supplier_id = @id
            GROUP  BY m.month
            ORDER  BY m.month;

            SELECT CASE WHEN count(*) = 0 THEN 0
                        ELSE count(*) FILTER (
                              WHERE p.price > 0
                                AND o.unit_price BETWEEN p.price * 0.8 AND p.price * 1.2
                             )::float8 / count(*)::float8
                   END                                                       AS price_consistency
            FROM   orders o
            JOIN   products p ON p.id = o.product_id
            WHERE  o.supplier_id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { id }, cancellationToken: ct));

        var existsMarker = await grid.ReadSingleAsync<long>();
        if (existsMarker == 0) return null;

        var aggregates = await grid.ReadSingleAsync<(long total_orders, double avg_delivery_days, double rejection_rate, decimal avg_order_value)>();
        var monthly = (await grid.ReadAsync<MonthlyTrendEntry>()).AsList();
        var priceConsistency = await grid.ReadSingleAsync<double>();

        return new SupplierPerformance(
            id,
            aggregates.total_orders,
            Math.Round(aggregates.avg_delivery_days, 2),
            Math.Round(aggregates.rejection_rate, 4),
            decimal.Round(aggregates.avg_order_value, 2),
            monthly,
            Math.Round(priceConsistency, 4));
    }
}
