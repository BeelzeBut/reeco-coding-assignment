using Microsoft.AspNetCore.Mvc;

namespace OrderOps.Api.Features.Products;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductsService _service;

    public ProductsController(ProductsService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken ct)
        => Ok(await _service.ListAsync(category, limit, offset, ct));
}
