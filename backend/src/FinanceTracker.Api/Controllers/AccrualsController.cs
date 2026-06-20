using FinanceTracker.Api.RateLimiting;
using FinanceTracker.Application.Common.Exceptions;
using FinanceTracker.Application.Features.Accruals;
using FinanceTracker.Application.Features.Export;
using FinanceTracker.Application.Features.Import;
using FinanceTracker.Application.Features.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

/// <summary>CRUD for accruals with pagination (T1.4.2). Receipt items (T1.4.7).</summary>
[ApiController]
[Authorize]
[Route("api/v1/accruals")]
public sealed class AccrualsController : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AccrualListItemDto>> GetPaged(
        [FromQuery] AccrualFilterRequest filter,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken) =>
        service.GetPagedAsync(filter, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<AccrualDto> GetById(
        Guid id,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken) =>
        service.GetByIdAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AccrualDto>> Create(
        [FromBody] CreateAccrualRequest request,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<AccrualDto> Update(
        Guid id,
        [FromBody] UpdateAccrualRequest request,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    // ── Async CSV export (Story 6.2 / T6.2.1) ────────────────────────────────

    /// <summary>
    /// Queues an async CSV export of the caller's accruals matching the given
    /// filters (the same filters as the list, minus pagination). Returns 202 with
    /// the job id; poll <c>GET /api/v1/jobs/{id}</c> and download via
    /// <c>GET /api/v1/jobs/{id}/result</c> once it is Done.
    /// </summary>
    [HttpPost("export")]
    public async Task<ActionResult<ExportJobResponse>> Export(
        [FromQuery] AccrualExportFilter filter,
        [FromServices] AccrualExportService service,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateExportAsync(filter, cancellationToken);
        return Accepted($"/api/v1/jobs/{response.JobId}", response);
    }

    // ── Async FNS import (Story 6.1 / T6.1.x) ────────────────────────────────

    /// <summary>
    /// Queues an async import of an FNS «Налоги ФЛ» receipts export (.xlsx).
    /// Returns 202 with the job id; poll <c>GET /api/v1/jobs/{id}</c> and download
    /// the summary via <c>GET /api/v1/jobs/{id}/result</c> once it is Done. Each
    /// receipt becomes an Expense accrual with a linked receipt + line items;
    /// receipts already present (by number + seller INN + date) are skipped.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImportJobResponse>> Import(
        IFormFile file,
        [FromServices] AccrualImportService service,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("Не приложен файл для импорта.");

        await using var stream = file.OpenReadStream();
        var response = await service.CreateImportAsync(stream, cancellationToken);
        return Accepted($"/api/v1/jobs/{response.JobId}", response);
    }

    // ── QR scan → background receipt fetch (Story 4.2 / T4.3.2) ───────────────

    /// <summary>
    /// Accepts a scanned receipt QR: creates the accrual + a Pending receipt and
    /// queues the fetch. Rate-limited tighter than the API default because each
    /// scan ultimately consumes the shared ≤15/day provider quota.
    /// </summary>
    [HttpPost("scan-qr")]
    [EnableRateLimiting(RateLimitingExtensions.ScanQrPolicy)]
    public async Task<ActionResult<ScanQrResponse>> ScanQr(
        [FromBody] ScanQrRequest request,
        [FromServices] ReceiptScanService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ScanAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.AccrualId }, result);
    }

    /// <summary>Polls the async fetch progress for an accrual's receipt (T4.3.3).</summary>
    [HttpGet("{id:guid}/receipt-status")]
    public Task<ReceiptStatusResponse> GetReceiptStatus(
        Guid id,
        [FromServices] ReceiptScanService service,
        CancellationToken cancellationToken) =>
        service.GetStatusAsync(id, cancellationToken);

    // ── Receipt items (T1.4.7) ───────────────────────────────────────────────

    [HttpGet("{id:guid}/receipt")]
    public Task<ReceiptDto> GetReceipt(
        Guid id,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken) =>
        service.GetReceiptAsync(id, cancellationToken);

    [HttpPost("{id:guid}/receipt")]
    public async Task<ActionResult<ReceiptDto>> CreateReceipt(
        Guid id,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken)
    {
        var receipt = await service.GetOrCreateReceiptAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetReceipt), new { id }, receipt);
    }

    [HttpPost("{id:guid}/receipt/items")]
    public async Task<ActionResult<ReceiptItemDto>> AddReceiptItem(
        Guid id,
        [FromBody] CreateReceiptItemRequest request,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken)
    {
        var item = await service.AddReceiptItemAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetReceipt), new { id }, item);
    }

    [HttpPut("{id:guid}/receipt/items/{itemId:guid}")]
    public Task<ReceiptItemDto> UpdateReceiptItem(
        Guid id,
        Guid itemId,
        [FromBody] UpdateReceiptItemRequest request,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken) =>
        service.UpdateReceiptItemAsync(id, itemId, request, cancellationToken);

    [HttpDelete("{id:guid}/receipt/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteReceiptItem(
        Guid id,
        Guid itemId,
        [FromServices] AccrualService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteReceiptItemAsync(id, itemId, cancellationToken);
        return NoContent();
    }
}
