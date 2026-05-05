using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Orders;

public sealed class OrdersService
{
    private readonly IOrderRepository _repo;

    public OrdersService(IOrderRepository repo) => _repo = repo;

    public Task<PagedResult<OrderListItem>> ListAsync(int? limit, int? offset, CancellationToken ct)
    {
        var (l, o) = Pagination.Normalize(limit, offset);
        return _repo.ListAsync(l, o, ct);
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
}
