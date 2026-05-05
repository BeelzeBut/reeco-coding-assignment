namespace OrderOps.Api.Features.Orders;

public sealed record OrderStats(
    long TotalOrders,
    decimal TotalRevenue,
    decimal AvgOrderValue,
    IReadOnlyDictionary<string, ByStatusBucket> ByStatus,
    IReadOnlyList<ByMonthBucket> ByMonth,
    IReadOnlyList<TopSupplier> TopSuppliers,
    IReadOnlyList<ByWarehouseBucket> ByWarehouse);

public sealed record ByStatusBucket(long Count, decimal TotalValue);

public sealed record ByMonthBucket(string Month, long OrderCount, decimal Revenue);

public sealed record TopSupplier(string SupplierId, string SupplierName, decimal TotalRevenue);

public sealed record ByWarehouseBucket(string Warehouse, long Count, decimal TotalValue);
