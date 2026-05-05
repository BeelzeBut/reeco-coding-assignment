namespace OrderOps.Api.Features.Suppliers;

public sealed record SupplierListItem(
    string Id,
    string Name,
    string? Email,
    decimal? Rating,
    string? Country,
    bool Active,
    DateTime CreatedAt);

public sealed record SupplierDetail(
    string Id,
    string Name,
    string? Email,
    decimal? Rating,
    string? Country,
    bool Active,
    DateTime CreatedAt,
    long OrderCount,
    decimal TotalRevenue);
