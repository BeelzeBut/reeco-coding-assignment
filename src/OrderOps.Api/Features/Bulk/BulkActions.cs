namespace OrderOps.Api.Features.Bulk;

public static class BulkActions
{
    public const string Approve = "approve";
    public const string Reject  = "reject";
    public const string Flag    = "flag";

    private static readonly HashSet<string> Valid = new(StringComparer.Ordinal) { Approve, Reject, Flag };

    public static bool IsValid(string? action) => action is not null && Valid.Contains(action);

    public static string? MapToStatus(string action) => action switch
    {
        Approve => "approved",
        Reject  => "rejected",
        Flag    => null,
        _       => null,
    };
}
