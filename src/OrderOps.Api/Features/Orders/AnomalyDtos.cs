namespace OrderOps.Api.Features.Orders;

public sealed record AnomalyItem(string OrderId, string[] AnomalyTypes, string Severity);

public sealed record AnomalyResponse(IReadOnlyList<AnomalyItem> Data);

public sealed class AnomalyRow
{
    public string OrderId { get; set; } = "";
    public string[] AnomalyTypes { get; set; } = Array.Empty<string>();
}
