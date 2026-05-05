using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Orders;

public interface IOrderRepository
{
    Task<PagedResult<OrderListItem>> ListAsync(int limit, int offset, CancellationToken ct);
    Task<OrderDetail?> GetByIdAsync(string id, CancellationToken ct);
    Task<UpdateStatusOutcome> UpdateStatusAsync(string id, string newStatus, CancellationToken ct);
}
