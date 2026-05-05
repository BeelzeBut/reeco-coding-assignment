namespace OrderOps.Api.Features.Orders;

public static class OrderPriorities
{
    public const string Critical = "critical";
    public const string High     = "high";
    public const string Medium   = "medium";
    public const string Low      = "low";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Critical, High, Medium, Low
    };

    public static bool IsValid(string? priority) => priority is not null && All.Contains(priority);
}
