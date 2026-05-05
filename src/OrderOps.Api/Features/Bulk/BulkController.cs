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

    [HttpPost("bulk-actions")]
    public async Task<IActionResult> SubmitSnakeCase([FromBody] BulkActionsRequest body, CancellationToken ct)
    {
        var canonical = new BulkActionRequest(body.OrderIds, body.Action, body.Reason);
        var response = await _service.EnqueueAsync(canonical, ct);
        return Accepted(new BulkActionsResponse(response.JobId));
    }
}
