namespace OrderOps.Api.Data.Entities;

public sealed class OrderFlag
{
    public string OrderId { get; set; } = "";
    public DateTime FlaggedAt { get; set; }
    public string? SourceJobId { get; set; }
    public string? Reason { get; set; }

    public Order Order { get; set; } = null!;
}
