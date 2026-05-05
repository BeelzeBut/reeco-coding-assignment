namespace OrderOps.Api.Features.Orders;

public sealed record OrderListItem(
    string Id,
    string SupplierId,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    string Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Warehouse,
    string? Notes);

public sealed record OrderDetail(
    string Id,
    string SupplierId,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    string Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Warehouse,
    string? Notes,
    string SupplierName,
    string ProductName);

public sealed record PatchOrderRequest(string? Status);

public abstract record UpdateStatusOutcome
{
    public sealed record NotFound : UpdateStatusOutcome;
    public sealed record AlreadyCancelled : UpdateStatusOutcome;
    public sealed record VersionConflict : UpdateStatusOutcome;
    public sealed record Updated(OrderDetail Order) : UpdateStatusOutcome;
}
