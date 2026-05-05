using Microsoft.AspNetCore.Mvc;

namespace OrderOps.Api.Features.Suppliers;

[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly SuppliersService _service;

    public SuppliersController(SuppliersService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
        => Ok(await _service.ListAsync(limit, offset, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("{id}/performance")]
    public async Task<IActionResult> Performance(string id, CancellationToken ct)
        => Ok(await _service.GetPerformanceAsync(id, ct));
}
