using Microsoft.AspNetCore.Mvc;

namespace OrderOps.Api.Features.Bulk;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly BulkService _service;

    public JobsController(BulkService service) => _service = service;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
        => Ok(await _service.GetJobAsync(id, ct));
}
