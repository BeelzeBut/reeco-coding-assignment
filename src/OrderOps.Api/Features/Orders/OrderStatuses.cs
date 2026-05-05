namespace OrderOps.Api.Features.Orders;

public static class OrderStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Shipped = "shipped";
    public const string Delivered = "delivered";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending, Approved, Rejected, Shipped, Delivered, Cancelled
    };

    public static bool IsValid(string? status) => status is not null && All.Contains(status);
}
