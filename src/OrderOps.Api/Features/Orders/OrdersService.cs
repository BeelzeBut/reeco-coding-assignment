using System.Globalization;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Orders;

public sealed class OrdersService
{
    private readonly IOrderRepository _repo;

    public OrdersService(IOrderRepository repo) => _repo = repo;

    public Task<PagedResult<OrderListItem>> ListAsync(OrderListRequest req, CancellationToken ct)
    {
        var (limit, offset) = Pagination.Normalize(req.Limit, req.Offset);
        var query = new OrderListQuery(
            ParseStatuses(req.Status),
            Trim(req.Priority),
            Trim(req.SupplierId),
            Trim(req.Warehouse),
            ParseDate(req.DateFrom, "date_from"),
            ParseDate(req.DateTo, "date_to"),
            req.MinTotal,
            Trim(req.Search),
            Trim(req.Sort),
            Trim(req.Order),
            limit,
            offset);
        return _repo.ListAsync(query, ct);
    }

    public async Task<OrderDetail> GetByIdAsync(string id, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(id, ct);
        if (order is null) throw new NotFoundException("Order");
        return order;
    }

    public async Task<OrderDetail> UpdateStatusAsync(string id, PatchOrderRequest body, CancellationToken ct)
    {
        if (body.Status is null)
            throw new ValidationException("status is required");
        if (!OrderStatuses.IsValid(body.Status))
            throw new ValidationException($"Invalid status '{body.Status}'", "invalid_status");

        var outcome = await _repo.UpdateStatusAsync(id, body.Status, ct);
        return outcome switch
        {
            UpdateStatusOutcome.Updated u => u.Order,
            UpdateStatusOutcome.NotFound => throw new NotFoundException("Order"),
            UpdateStatusOutcome.AlreadyCancelled => throw new OrderAlreadyCancelledException(),
            UpdateStatusOutcome.VersionConflict => throw new OptimisticConcurrencyException(),
            _ => throw new InvalidOperationException("Unknown outcome")
        };
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IReadOnlyList<string>? ParseStatuses(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts;
    }

    private static DateTime? ParseDate(string? raw, string field)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, styles, out var d))
            return d;
        throw new ValidationException($"Invalid {field}: '{raw}'", "invalid_date");
    }
}
