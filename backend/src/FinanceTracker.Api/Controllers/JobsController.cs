using FinanceTracker.Api.RateLimiting;
using FinanceTracker.Application.Features.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

/// <summary>
/// Async import/export job status and result download (T6.1.3 / T6.2.2).
/// Ownership is enforced in the service via the data-isolation query filter; the
/// result is streamed through the API from the private bucket — never a presigned
/// URL — and the download is rate-limited.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/jobs")]
public sealed class JobsController : ControllerBase
{
    /// <summary>Polls the status/progress of an async job.</summary>
    [HttpGet("{id:guid}")]
    public Task<BackgroundTaskStatusDto> GetStatus(
        Guid id,
        [FromServices] BackgroundTaskService service,
        CancellationToken cancellationToken) =>
        service.GetStatusAsync(id, cancellationToken);

    /// <summary>Streams the finished job's result file (e.g. the export CSV).</summary>
    [HttpGet("{id:guid}/result")]
    [EnableRateLimiting(RateLimitingExtensions.JobResultPolicy)]
    public async Task<IActionResult> GetResult(
        Guid id,
        [FromServices] BackgroundTaskService service,
        CancellationToken cancellationToken)
    {
        var result = await service.OpenResultAsync(id, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
