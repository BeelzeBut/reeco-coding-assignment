namespace OrderOps.Api.Features.Products;

public sealed record ProductListItem(
    string Id,
    string Name,
    string? CategoryId,
    string? Sku,
    decimal Price);
