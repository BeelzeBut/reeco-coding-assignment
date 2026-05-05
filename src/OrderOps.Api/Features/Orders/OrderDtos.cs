using Microsoft.AspNetCore.Mvc;

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
    string? Notes,
    string ProductName,
    DateTime? FlaggedAt,
    string? FlagReason);

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
    string ProductName,
    DateTime? FlaggedAt,
    string? FlagReason);

public sealed record PatchOrderRequest(string? Status, string? Priority, string? Notes);

public sealed record OrderUpdate(string? Status, string? Priority, string? Notes);

public sealed class OrderListRequest
{
    [FromQuery(Name = "status")]      public string?  Status      { get; set; }
    [FromQuery(Name = "priority")]    public string?  Priority    { get; set; }
    [FromQuery(Name = "supplier_id")] public string?  SupplierId  { get; set; }
    [FromQuery(Name = "warehouse")]   public string?  Warehouse   { get; set; }
    [FromQuery(Name = "date_from")]   public string?  DateFrom    { get; set; }
    [FromQuery(Name = "date_to")]     public string?  DateTo      { get; set; }
    [FromQuery(Name = "min_total")]   public decimal? MinTotal    { get; set; }
    [FromQuery(Name = "flagged")]     public bool?    Flagged     { get; set; }
    [FromQuery(Name = "search")]      public string?  Search      { get; set; }
    [FromQuery(Name = "sort")]        public string?  Sort        { get; set; }
    [FromQuery(Name = "order")]       public string?  Order       { get; set; }
    [FromQuery(Name = "limit")]       public int?     Limit       { get; set; }
    [FromQuery(Name = "offset")]      public int?     Offset      { get; set; }
}

public sealed record OrderListQuery(
    IReadOnlyList<string>? Statuses,
    string? Priority,
    string? SupplierId,
    string? Warehouse,
    DateTime? DateFrom,
    DateTime? DateTo,
    decimal? MinTotal,
    bool? Flagged,
    string? Search,
    string? Sort,
    string? Order,
    int Limit,
    int Offset);

public abstract record UpdateStatusOutcome
{
    public sealed record NotFound : UpdateStatusOutcome;
    public sealed record AlreadyCancelled : UpdateStatusOutcome;
    public sealed record VersionConflict : UpdateStatusOutcome;
    public sealed record Updated(OrderDetail Order, string OldStatus) : UpdateStatusOutcome;
}
