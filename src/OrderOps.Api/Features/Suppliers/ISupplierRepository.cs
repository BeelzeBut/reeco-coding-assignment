using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Suppliers;

public interface ISupplierRepository
{
    Task<PagedResult<SupplierListItem>> ListAsync(int limit, int offset, CancellationToken ct);
    Task<SupplierDetail?> GetByIdAsync(string id, CancellationToken ct);
}
