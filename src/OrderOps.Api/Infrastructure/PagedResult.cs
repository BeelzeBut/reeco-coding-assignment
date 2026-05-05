namespace OrderOps.Api.Infrastructure;

public sealed record PagedResult<T>(IReadOnlyList<T> Data, long Total, int Limit, int Offset);

public static class Pagination
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 1000;

    public static (int limit, int offset) Normalize(int? limit, int? offset)
    {
        var l = limit ?? DefaultLimit;
        if (l < 1) l = 1;
        if (l > MaxLimit) l = MaxLimit;

        var o = offset ?? 0;
        if (o < 0) o = 0;

        return (l, o);
    }
}
