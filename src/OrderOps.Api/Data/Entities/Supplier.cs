namespace OrderOps.Api.Data.Entities;

public sealed class Supplier
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public decimal? Rating { get; set; }
    public string? Country { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
