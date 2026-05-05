using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Products;

public sealed class ProductsService
{
    private readonly IProductRepository _repo;

    public ProductsService(IProductRepository repo) => _repo = repo;

    public Task<PagedResult<ProductListItem>> ListAsync(
        string? category, int? limit, int? offset, CancellationToken ct)
    {
        var (l, o) = Pagination.Normalize(limit, offset);
        return string.IsNullOrEmpty(category)
            ? _repo.ListAsync(l, o, ct)
            : _repo.ListByCategoryDescendantsAsync(category, l, o, ct);
    }
}
