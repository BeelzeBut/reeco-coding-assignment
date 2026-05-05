using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Suppliers;

public sealed class SuppliersService
{
    private readonly ISupplierRepository _repo;

    public SuppliersService(ISupplierRepository repo) => _repo = repo;

    public Task<PagedResult<SupplierListItem>> ListAsync(int? limit, int? offset, CancellationToken ct)
    {
        var (l, o) = Pagination.Normalize(limit, offset);
        return _repo.ListAsync(l, o, ct);
    }

    public async Task<SupplierDetail> GetByIdAsync(string id, CancellationToken ct)
    {
        var supplier = await _repo.GetByIdAsync(id, ct);
        if (supplier is null) throw new NotFoundException("Supplier");
        return supplier;
    }

    public async Task<SupplierPerformance> GetPerformanceAsync(string id, CancellationToken ct)
    {
        var perf = await _repo.GetPerformanceAsync(id, ct);
        if (perf is null) throw new NotFoundException("Supplier");
        return perf;
    }
}
