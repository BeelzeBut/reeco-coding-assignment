using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Products;

public interface IProductRepository
{
    Task<PagedResult<ProductListItem>> ListAsync(int limit, int offset, CancellationToken ct);
    Task<PagedResult<ProductListItem>> ListByCategoryDescendantsAsync(
        string rootCategoryId, int limit, int offset, CancellationToken ct);
}
