using System.Text.Json;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Common;

/// <summary>
/// Implements the Idempotency-Key pattern (ARCHITECTURE.md §4): POST/PUT/DELETE
/// requests that supply the header are deduplicated — a second request with the
/// same key returns the stored response without re-executing the action.
/// Records expire after 24 h.
/// </summary>
public sealed class IdempotencyFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> ModifyingMethods = ["POST", "PUT", "DELETE", "PATCH"];

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (!ModifyingMethods.Contains(method))
        {
            await next();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues))
        {
            await next();
            return;
        }

        var key = keyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;
        var db = services.GetRequiredService<IApplicationDbContext>();
        var currentUser = services.GetRequiredService<ICurrentUserService>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        if (currentUser.UserId is null)
        {
            await next();
            return;
        }

        var userId = currentUser.UserId.Value;
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        var existing = await db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == key && r.UserId == userId && r.RequestPath == path);

        if (existing is not null && existing.ExpiresAt > DateTimeOffset.UtcNow)
        {
            context.Result = new ContentResult
            {
                Content = existing.ResponseBody,
                ContentType = "application/json",
                StatusCode = existing.StatusCode,
            };
            return;
        }

        var executed = await next();

        if (executed.Exception is not null || executed.Canceled)
            return;

        string responseBody;
        int statusCode;

        switch (executed.Result)
        {
            case ObjectResult { Value: not null } obj:
                responseBody = JsonSerializer.Serialize(obj.Value, JsonOptions);
                statusCode = obj.StatusCode ?? 200;
                break;
            case NoContentResult:
                responseBody = string.Empty;
                statusCode = 204;
                break;
            default:
                return;
        }

        // Upsert: replace an expired record for the same key rather than inserting a duplicate
        if (existing is not null)
            db.IdempotencyRecords.Remove(existing);

        db.IdempotencyRecords.Add(new IdempotencyRecord(key, userId, method, path, statusCode, responseBody));
        await unitOfWork.SaveChangesAsync(default);
    }
}
