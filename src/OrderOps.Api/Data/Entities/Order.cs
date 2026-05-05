namespace OrderOps.Api.Data.Entities;

public sealed class Order
{
    public string Id { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Warehouse { get; set; }
    public string? Notes { get; set; }
    public int Version { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public OrderFlag? Flag { get; set; }
}
