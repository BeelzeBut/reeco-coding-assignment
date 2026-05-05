namespace OrderOps.Api.Features.Suppliers;

public sealed record SupplierPerformance(
    string SupplierId,
    long TotalOrders,
    double AvgDeliveryDays,
    double RejectionRate,
    decimal AvgOrderValue,
    IReadOnlyList<MonthlyTrendEntry> MonthlyTrend,
    double PriceConsistency);

public sealed record MonthlyTrendEntry(string Month, long OrderCount, decimal Revenue);
