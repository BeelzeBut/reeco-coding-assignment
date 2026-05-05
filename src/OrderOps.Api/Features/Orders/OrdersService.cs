using System.Globalization;
using OrderOps.Api.Features.Events;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Orders;

public sealed class OrdersService
{
    private readonly IOrderRepository _repo;
    private readonly IEventHub _events;
    private readonly ILogger<OrdersService> _log;

    public OrdersService(IOrderRepository repo, IEventHub events, ILogger<OrdersService> log)
    {
        _repo = repo;
        _events = events;
        _log = log;
    }

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
            req.Flagged,
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

    public Task<OrderStats> GetStatsAsync(CancellationToken ct) => _repo.GetStatsAsync(ct);

    public async Task<AnomalyResponse> GetAnomaliesAsync(CancellationToken ct)
    {
        var rows = await _repo.GetAnomalousAsync(ct);
        var items = new List<AnomalyItem>(rows.Count);
        foreach (var r in rows)
            items.Add(new AnomalyItem(r.OrderId, r.AnomalyTypes, DetermineSeverity(r.AnomalyTypes)));
        return new AnomalyResponse(items);
    }

    private static readonly HashSet<string> HighSingle = new() { "negative_quantity", "price_mismatch", "timestamp_anomaly" };
    private static readonly HashSet<string> MediumSingle = new() { "inactive_supplier", "price_spike" };

    private static string DetermineSeverity(string[] types)
    {
        if (types.Length >= 3) return "high";
        foreach (var t in types) if (t == "risky_supplier") return "high";
        foreach (var t in types) if (HighSingle.Contains(t)) return "high";
        if (types.Length == 2) return "medium";
        foreach (var t in types) if (MediumSingle.Contains(t)) return "medium";
        return "low";
    }

    private const int MaxNotesLength = 4096;

    public async Task<OrderDetail> UpdateStatusAsync(string id, PatchOrderRequest body, CancellationToken ct)
    {
        var status   = Trim(body.Status);
        var priority = Trim(body.Priority);
        var notes    = body.Notes; // notes preserved verbatim; only length-capped

        if (status is null && priority is null && notes is null)
            throw new ValidationException("at least one of status, priority, notes is required", "no_fields");

        if (status is not null && !OrderStatuses.IsValid(status))
            throw new ValidationException($"Invalid status '{status}'", "invalid_status");
        if (priority is not null && !OrderPriorities.IsValid(priority))
            throw new ValidationException($"Invalid priority '{priority}'", "invalid_priority");
        if (notes is not null && notes.Length > MaxNotesLength)
            throw new ValidationException($"notes exceeds {MaxNotesLength} characters", "notes_too_long");

        var outcome = await _repo.UpdateAsync(id, new OrderUpdate(status, priority, notes), ct);
        switch (outcome)
        {
            case UpdateStatusOutcome.Updated u:
                if (!string.Equals(u.OldStatus, u.Order.Status, StringComparison.Ordinal))
                {
                    try
                    {
                        await _events.PublishAsync(new EventEnvelope(
                            "order_updated",
                            new OrderUpdatedPayload(u.Order.Id, u.OldStatus, u.Order.Status, u.Order.UpdatedAt),
                            u.Order.SupplierId));
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Failed to publish order_updated for {OrderId}", u.Order.Id);
                    }
                }
                return u.Order;
            case UpdateStatusOutcome.NotFound: throw new NotFoundException("Order");
            case UpdateStatusOutcome.AlreadyCancelled: throw new OrderAlreadyCancelledException();
            case UpdateStatusOutcome.VersionConflict: throw new OptimisticConcurrencyException();
            default: throw new InvalidOperationException("Unknown outcome");
        }
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
