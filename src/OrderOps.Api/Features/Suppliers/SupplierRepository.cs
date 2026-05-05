using Microsoft.EntityFrameworkCore;
using OrderOps.Api.Data;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Suppliers;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _db;

    public SupplierRepository(AppDbContext db) => _db = db;

    public async Task<PagedResult<SupplierListItem>> ListAsync(int limit, int offset, CancellationToken ct)
    {
        var total = await _db.Suppliers.CountAsync(ct);

        var rows = await _db.Suppliers.AsNoTracking()
            .OrderBy(s => s.Id)
            .Skip(offset).Take(limit)
            .Select(s => new SupplierListItem(
                s.Id, s.Name, s.Email, s.Rating, s.Country, s.Active, s.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<SupplierListItem>(rows, total, limit, offset);
    }

    public async Task<SupplierDetail?> GetByIdAsync(string id, CancellationToken ct)
    {
        return await _db.Suppliers.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierDetail(
                s.Id, s.Name, s.Email, s.Rating, s.Country, s.Active, s.CreatedAt,
                s.Orders.Count(),
                s.Orders.Sum(o => (decimal?)o.TotalPrice) ?? 0m))
            .FirstOrDefaultAsync(ct);
    }

    private static readonly DateTime SeedRangeStart = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly int SeedMonthCount = 24;

    public async Task<SupplierPerformance?> GetPerformanceAsync(string id, CancellationToken ct)
    {
        var exists = await _db.Suppliers.AsNoTracking().AnyAsync(s => s.Id == id, ct);
        if (!exists) return null;

        var supplierOrders = _db.Orders.AsNoTracking().Where(o => o.SupplierId == id);

        var totalOrders = await supplierOrders.LongCountAsync(ct);

        double avgDeliveryDays = 0;
        if (totalOrders > 0)
        {
            var deliveredTimes = await supplierOrders
                .Where(o => o.Status == "delivered")
                .Select(o => new { o.CreatedAt, o.UpdatedAt })
                .ToListAsync(ct);
            if (deliveredTimes.Count > 0)
                avgDeliveryDays = deliveredTimes.Average(t => (t.UpdatedAt - t.CreatedAt).TotalDays);
        }

        double rejectionRate = 0;
        if (totalOrders > 0)
        {
            var rejectedCount = await supplierOrders.Where(o => o.Status == "rejected").LongCountAsync(ct);
            rejectionRate = (double)rejectedCount / totalOrders;
        }

        decimal avgOrderValue = 0;
        if (totalOrders > 0)
        {
            avgOrderValue = await supplierOrders.AverageAsync(o => o.TotalPrice, ct);
        }

        var monthlyRaw = await supplierOrders
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.LongCount(), Revenue = g.Sum(o => (decimal?)o.TotalPrice) ?? 0m })
            .ToListAsync(ct);

        var monthlyByKey = monthlyRaw.ToDictionary(
            r => $"{r.Year:D4}-{r.Month:D2}",
            r => new MonthlyTrendEntry($"{r.Year:D4}-{r.Month:D2}", r.Count, r.Revenue));

        var monthly = new List<MonthlyTrendEntry>(SeedMonthCount);
        for (int i = 0; i < SeedMonthCount; i++)
        {
            var d = SeedRangeStart.AddMonths(i);
            var key = $"{d.Year:D4}-{d.Month:D2}";
            monthly.Add(monthlyByKey.TryGetValue(key, out var entry)
                ? entry
                : new MonthlyTrendEntry(key, 0, 0m));
        }

        double priceConsistency = 0;
        if (totalOrders > 0)
        {
            var consistencyRaw = await (
                from o in _db.Orders
                join p in _db.Products on o.ProductId equals p.Id
                where o.SupplierId == id
                select new { o.UnitPrice, p.Price })
                .ToListAsync(ct);

            if (consistencyRaw.Count > 0)
            {
                var within = consistencyRaw.Count(x =>
                    x.Price > 0 && x.UnitPrice >= x.Price * 0.8m && x.UnitPrice <= x.Price * 1.2m);
                priceConsistency = (double)within / consistencyRaw.Count;
            }
        }

        return new SupplierPerformance(
            id,
            totalOrders,
            Math.Round(avgDeliveryDays, 2),
            Math.Round(rejectionRate, 4),
            decimal.Round(avgOrderValue, 2),
            monthly,
            Math.Round(priceConsistency, 4));
    }
}
