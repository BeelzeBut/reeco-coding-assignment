using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Orders;

public interface IOrderRepository
{
    Task<PagedResult<OrderListItem>> ListAsync(OrderListQuery query, CancellationToken ct);
    Task<OrderDetail?> GetByIdAsync(string id, CancellationToken ct);
    Task<UpdateStatusOutcome> UpdateAsync(string id, OrderUpdate update, CancellationToken ct);
    Task<OrderStats> GetStatsAsync(CancellationToken ct);
    Task<IReadOnlyList<AnomalyRow>> GetAnomalousAsync(CancellationToken ct);
}
