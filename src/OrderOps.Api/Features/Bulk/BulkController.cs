using Microsoft.AspNetCore.Mvc;

namespace OrderOps.Api.Features.Bulk;

[ApiController]
[Route("api/orders")]
public sealed class BulkController : ControllerBase
{
    private readonly BulkService _service;

    public BulkController(BulkService service) => _service = service;

    [HttpPost("bulk-action")]
    public async Task<IActionResult> Submit([FromBody] BulkActionRequest body, CancellationToken ct)
    {
        var response = await _service.EnqueueAsync(body, ct);
        return Accepted(response);
    }
}
