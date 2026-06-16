using FinanceTracker.Application.Features.Accruals;
using FinanceTracker.Application.Features.ChangeLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

/// <summary>Read-only change log with pagination and entity-type filter (Epic 7, T7.1.1).</summary>
[ApiController]
[Authorize]
[Route("api/v1/changelog")]
public sealed class ChangeLogController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ChangeLogDto>> GetPaged(
        [FromQuery] ChangeLogFilterRequest filter,
        [FromServices] ChangeLogService service,
        CancellationToken cancellationToken) =>
        service.GetPagedAsync(filter, cancellationToken);
}
