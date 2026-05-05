namespace OrderOps.Api.Data.Entities;

public sealed class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? CategoryId { get; set; }
    public string? Sku { get; set; }
    public decimal Price { get; set; }

    public Category? Category { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
