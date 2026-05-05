using Microsoft.EntityFrameworkCore;
using OrderOps.Api.Data;
using OrderOps.Api.Data.Entities;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Orders;

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    private static readonly IReadOnlyDictionary<string, string> SortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "Id",
            ["created_at"] = "CreatedAt",
            ["updated_at"] = "UpdatedAt",
            ["total_price"] = "TotalPrice",
            ["unit_price"] = "UnitPrice",
            ["quantity"] = "Quantity",
            ["status"] = "Status",
            ["priority"] = "Priority",
            ["supplier_id"] = "SupplierId",
            ["warehouse"] = "Warehouse",
        };

    public async Task<PagedResult<OrderListItem>> ListAsync(OrderListQuery q, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking().Include(o => o.Product).AsQueryable();

        if (q.Statuses is { Count: > 0 })
        {
            var statuses = q.Statuses.ToArray();
            query = query.Where(o => statuses.Contains(o.Status));
        }
        if (!string.IsNullOrEmpty(q.Priority))
            query = query.Where(o => o.Priority == q.Priority);
        if (!string.IsNullOrEmpty(q.SupplierId))
            query = query.Where(o => o.SupplierId == q.SupplierId);
        if (!string.IsNullOrEmpty(q.Warehouse))
            query = query.Where(o => o.Warehouse == q.Warehouse);
        if (q.DateFrom.HasValue)
            query = query.Where(o => o.CreatedAt >= q.DateFrom.Value);
        if (q.DateTo.HasValue)
        {
            var upper = q.DateTo.Value.AddDays(1);
            query = query.Where(o => o.CreatedAt < upper);
        }
        if (q.MinTotal.HasValue)
            query = query.Where(o => o.TotalPrice >= q.MinTotal.Value);
        if (!string.IsNullOrEmpty(q.Search))
        {
            var pattern = "%" + q.Search + "%";
            query = query.Where(o => EF.Functions.ILike(o.Product.Name, pattern));
        }

        var sortKey = q.Sort is not null && SortColumns.TryGetValue(q.Sort, out var c) ? c : "Id";
        var desc = string.Equals(q.Order, "desc", StringComparison.OrdinalIgnoreCase);
        query = ApplySort(query, sortKey, desc);

        var total = await query.LongCountAsync(ct);

        var rows = await query
            .Skip(q.Offset).Take(q.Limit)
            .Select(o => new OrderListItem(
                o.Id, o.SupplierId, o.ProductId, o.Quantity, o.UnitPrice, o.TotalPrice,
                o.Status, o.Priority, o.CreatedAt, o.UpdatedAt, o.Warehouse, o.Notes,
                o.Product.Name,
                o.Flag != null ? (DateTime?)o.Flag.FlaggedAt : null,
                o.Flag != null ? o.Flag.Reason : null))
            .ToListAsync(ct);

        return new PagedResult<OrderListItem>(rows, total, q.Limit, q.Offset);
    }

    private static IQueryable<Order> ApplySort(IQueryable<Order> q, string key, bool desc) => key switch
    {
        "CreatedAt"   => (desc ? q.OrderByDescending(o => o.CreatedAt)   : q.OrderBy(o => o.CreatedAt)).ThenBy(o => o.Id),
        "UpdatedAt"   => (desc ? q.OrderByDescending(o => o.UpdatedAt)   : q.OrderBy(o => o.UpdatedAt)).ThenBy(o => o.Id),
        "TotalPrice"  => (desc ? q.OrderByDescending(o => o.TotalPrice)  : q.OrderBy(o => o.TotalPrice)).ThenBy(o => o.Id),
        "UnitPrice"   => (desc ? q.OrderByDescending(o => o.UnitPrice)   : q.OrderBy(o => o.UnitPrice)).ThenBy(o => o.Id),
        "Quantity"    => (desc ? q.OrderByDescending(o => o.Quantity)    : q.OrderBy(o => o.Quantity)).ThenBy(o => o.Id),
        "Status"      => (desc ? q.OrderByDescending(o => o.Status)      : q.OrderBy(o => o.Status)).ThenBy(o => o.Id),
        "Priority"    => (desc ? q.OrderByDescending(o => o.Priority)    : q.OrderBy(o => o.Priority)).ThenBy(o => o.Id),
        "SupplierId"  => (desc ? q.OrderByDescending(o => o.SupplierId)  : q.OrderBy(o => o.SupplierId)).ThenBy(o => o.Id),
        "Warehouse"   => (desc ? q.OrderByDescending(o => o.Warehouse)   : q.OrderBy(o => o.Warehouse)).ThenBy(o => o.Id),
        _             => desc ? q.OrderByDescending(o => o.Id) : q.OrderBy(o => o.Id),
    };

    public Task<OrderDetail?> GetByIdAsync(string id, CancellationToken ct)
        => GetDetailAsync(id, ct);

    private async Task<OrderDetail?> GetDetailAsync(string id, CancellationToken ct)
    {
        return await _db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDetail(
                o.Id, o.SupplierId, o.ProductId, o.Quantity, o.UnitPrice, o.TotalPrice,
                o.Status, o.Priority, o.CreatedAt, o.UpdatedAt, o.Warehouse, o.Notes,
                o.Supplier.Name, o.Product.Name,
                o.Flag != null ? (DateTime?)o.Flag.FlaggedAt : null,
                o.Flag != null ? o.Flag.Reason : null))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UpdateStatusOutcome> UpdateAsync(string id, OrderUpdate update, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return new UpdateStatusOutcome.NotFound();
        if (order.Status == OrderStatuses.Cancelled) return new UpdateStatusOutcome.AlreadyCancelled();

        var oldStatus = order.Status;
        if (update.Status is not null) order.Status = update.Status;
        if (update.Priority is not null) order.Priority = update.Priority;
        if (update.Notes is not null) order.Notes = update.Notes;
        order.UpdatedAt = DateTime.UtcNow;
        order.Version += 1;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateStatusOutcome.VersionConflict();
        }

        var detail = await GetDetailAsync(id, ct)
            ?? throw new InvalidOperationException($"Order {id} not found after update");
        return new UpdateStatusOutcome.Updated(detail, oldStatus);
    }

    public async Task<OrderStats> GetStatsAsync(CancellationToken ct)
    {
        var orders = _db.Orders.AsNoTracking();

        var totals = await orders
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.LongCount(),
                Total = g.Sum(o => (decimal?)o.TotalPrice) ?? 0m,
                Avg = g.Average(o => (decimal?)o.TotalPrice) ?? 0m,
            })
            .FirstOrDefaultAsync(ct)
            ?? new { Count = 0L, Total = 0m, Avg = 0m };

        var byStatusRows = await orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.LongCount(), Total = g.Sum(o => (decimal?)o.TotalPrice) ?? 0m })
            .ToListAsync(ct);

        var byMonthRaw = await orders
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.LongCount(), Revenue = g.Sum(o => (decimal?)o.TotalPrice) ?? 0m })
            .ToListAsync(ct);

        var byMonth = byMonthRaw
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .Select(r => new ByMonthBucket($"{r.Year:D4}-{r.Month:D2}", r.Count, r.Revenue))
            .ToList();

        var topSuppliers = await orders
            .GroupBy(o => o.SupplierId)
            .Select(g => new { SupplierId = g.Key, TotalRevenue = g.Sum(o => (decimal?)o.TotalPrice) ?? 0m })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(10)
            .Join(_db.Suppliers, x => x.SupplierId, s => s.Id, (x, s) => new TopSupplier(x.SupplierId, s.Name, x.TotalRevenue))
            .ToListAsync(ct);

        var byWarehouseRaw = await orders
            .GroupBy(o => o.Warehouse)
            .Select(g => new { Warehouse = g.Key, Count = g.LongCount(), Total = g.Sum(o => (decimal?)o.TotalPrice) ?? 0m })
            .ToListAsync(ct);

        var byWarehouse = byWarehouseRaw
            .Select(r => new ByWarehouseBucket(r.Warehouse ?? "unassigned", r.Count, r.Total))
            .OrderBy(b => b.Warehouse, StringComparer.Ordinal)
            .ToList();

        var byStatus = byStatusRows.ToDictionary(
            r => r.Status,
            r => new ByStatusBucket(r.Count, r.Total));

        return new OrderStats(
            totals.Count,
            totals.Total,
            decimal.Round(totals.Avg, 2),
            byStatus,
            byMonth,
            topSuppliers,
            byWarehouse);
    }

    public async Task<IReadOnlyList<AnomalyRow>> GetAnomalousAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT o.id AS "OrderId",
                   array_remove(ARRAY[
                     CASE WHEN abs(o.total_price - (o.quantity * o.unit_price)) > 0.01 THEN 'price_mismatch' END,
                     CASE WHEN o.quantity < 0                                          THEN 'negative_quantity' END,
                     CASE WHEN o.updated_at < o.created_at                             THEN 'timestamp_anomaly' END,
                     CASE WHEN s.active = false                                        THEN 'inactive_supplier' END,
                     CASE WHEN o.unit_price > p.price * 1.5                            THEN 'price_spike' END,
                     CASE WHEN extract(hour FROM o.created_at) < 8
                            OR extract(hour FROM o.created_at) >= 18                   THEN 'after_hours' END,
                     CASE WHEN s.rating <= 1.5                                         THEN 'risky_supplier' END
                   ], NULL) AS "AnomalyTypes"
            FROM   orders o
            JOIN   suppliers s ON s.id = o.supplier_id
            JOIN   products  p ON p.id = o.product_id
            WHERE  abs(o.total_price - (o.quantity * o.unit_price)) > 0.01
               OR  o.quantity < 0
               OR  o.updated_at < o.created_at
               OR  s.active = false
               OR  o.unit_price > p.price * 1.5
               OR  extract(hour FROM o.created_at) < 8
               OR  extract(hour FROM o.created_at) >= 18
               OR  s.rating <= 1.5
            ORDER  BY o.id
            """;

        var rows = await _db.Database.SqlQueryRaw<AnomalyRow>(sql).ToListAsync(ct);
        return rows;
    }
}
