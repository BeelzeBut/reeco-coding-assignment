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

    private const string ListColumns = """
        o.id, o.supplier_id, o.product_id, o.quantity, o.unit_price, o.total_price,
        o.status, o.priority, o.created_at, o.updated_at, o.warehouse, o.notes,
        p.name AS product_name
        """;

    // Whitelist: API field name → SQL column. Anything not in this map falls back to o.id.
    private static readonly IReadOnlyDictionary<string, string> SortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"]          = "o.id",
            ["created_at"]  = "o.created_at",
            ["updated_at"]  = "o.updated_at",
            ["total_price"] = "o.total_price",
            ["unit_price"]  = "o.unit_price",
            ["quantity"]    = "o.quantity",
            ["status"]      = "o.status",
            ["priority"]    = "o.priority",
            ["supplier_id"] = "o.supplier_id",
            ["warehouse"]   = "o.warehouse",
        };

    public async Task<PagedResult<OrderListItem>> ListAsync(OrderListQuery q, CancellationToken ct)
    {
        var (where, parameters) = BuildWhere(q);
        var sortCol = q.Sort is not null && SortColumns.TryGetValue(q.Sort, out var c) ? c : "o.id";
        var sortDir = string.Equals(q.Order, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var countJoin = string.IsNullOrEmpty(q.Search) ? "" : "JOIN products p ON p.id = o.product_id";

        parameters.Add("limit", q.Limit);
        parameters.Add("offset", q.Offset);

        var sql = $"""
            SELECT {ListColumns}
            FROM   orders o
            JOIN   products p ON p.id = o.product_id
            {where}
            ORDER  BY {sortCol} {sortDir}, o.id
            LIMIT  @limit OFFSET @offset;

            SELECT count(*)
            FROM   orders o
            {countJoin}
            {where};
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, parameters, cancellationToken: ct));

        var rows = (await grid.ReadAsync<OrderListItem>()).AsList();
        var total = await grid.ReadSingleAsync<long>();
        return new PagedResult<OrderListItem>(rows, total, q.Limit, q.Offset);
    }

    private static (string Where, DynamicParameters P) BuildWhere(OrderListQuery q)
    {
        var clauses = new List<string>();
        var p = new DynamicParameters();

        if (q.Statuses is { Count: > 0 })
        {
            clauses.Add("o.status = ANY(@statuses)");
            p.Add("statuses", q.Statuses.ToArray());
        }
        if (!string.IsNullOrEmpty(q.Priority))
        {
            clauses.Add("o.priority = @priority");
            p.Add("priority", q.Priority);
        }
        if (!string.IsNullOrEmpty(q.SupplierId))
        {
            clauses.Add("o.supplier_id = @supplier_id");
            p.Add("supplier_id", q.SupplierId);
        }
        if (!string.IsNullOrEmpty(q.Warehouse))
        {
            clauses.Add("o.warehouse = @warehouse");
            p.Add("warehouse", q.Warehouse);
        }
        if (q.DateFrom.HasValue)
        {
            clauses.Add("o.created_at >= @date_from");
            p.Add("date_from", q.DateFrom.Value);
        }
        if (q.DateTo.HasValue)
        {
            clauses.Add("o.created_at < @date_to + interval '1 day'");
            p.Add("date_to", q.DateTo.Value);
        }
        if (q.MinTotal.HasValue)
        {
            clauses.Add("o.total_price >= @min_total");
            p.Add("min_total", q.MinTotal.Value);
        }
        if (!string.IsNullOrEmpty(q.Search))
        {
            clauses.Add("p.name ILIKE '%' || @search || '%'");
            p.Add("search", q.Search);
        }

        var where = clauses.Count == 0 ? "" : "WHERE " + string.Join(" AND ", clauses);
        return (where, p);
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

    public async Task<OrderStats> GetStatsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT count(*)                              AS total_orders,
                   COALESCE(sum(total_price), 0)         AS total_revenue,
                   COALESCE(avg(total_price), 0)         AS avg_order_value
            FROM   orders;

            SELECT status,
                   count(*)                              AS count,
                   COALESCE(sum(total_price), 0)         AS total_value
            FROM   orders
            GROUP  BY status;

            SELECT to_char(date_trunc('month', created_at), 'YYYY-MM') AS month,
                   count(*)                              AS order_count,
                   COALESCE(sum(total_price), 0)         AS revenue
            FROM   orders
            GROUP  BY date_trunc('month', created_at)
            ORDER  BY date_trunc('month', created_at);

            SELECT o.supplier_id,
                   s.name                                AS supplier_name,
                   COALESCE(sum(o.total_price), 0)       AS total_revenue
            FROM   orders o
            JOIN   suppliers s ON s.id = o.supplier_id
            GROUP  BY o.supplier_id, s.name
            ORDER  BY total_revenue DESC
            LIMIT  10;

            SELECT COALESCE(warehouse, 'unassigned')     AS warehouse,
                   count(*)                              AS count,
                   COALESCE(sum(total_price), 0)         AS total_value
            FROM   orders
            GROUP  BY COALESCE(warehouse, 'unassigned')
            ORDER  BY warehouse;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, cancellationToken: ct));

        var totals = await grid.ReadSingleAsync<(long total_orders, decimal total_revenue, decimal avg_order_value)>();
        var byStatusRows = (await grid.ReadAsync<(string status, long count, decimal total_value)>()).AsList();
        var byMonth = (await grid.ReadAsync<ByMonthBucket>()).AsList();
        var topSuppliers = (await grid.ReadAsync<TopSupplier>()).AsList();
        var byWarehouse = (await grid.ReadAsync<ByWarehouseBucket>()).AsList();

        var byStatus = byStatusRows.ToDictionary(
            r => r.status,
            r => new ByStatusBucket(r.count, r.total_value));

        return new OrderStats(
            totals.total_orders,
            totals.total_revenue,
            decimal.Round(totals.avg_order_value, 2),
            byStatus,
            byMonth,
            topSuppliers,
            byWarehouse);
    }
}
