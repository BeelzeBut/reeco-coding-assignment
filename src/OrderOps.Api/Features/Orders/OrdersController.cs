using Microsoft.AspNetCore.Mvc;

namespace OrderOps.Api.Features.Orders;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrdersService _service;

    public OrdersController(OrdersService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
        => Ok(await _service.ListAsync(limit, offset, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(string id, [FromBody] PatchOrderRequest body, CancellationToken ct)
        => Ok(await _service.UpdateStatusAsync(id, body, ct));
}
