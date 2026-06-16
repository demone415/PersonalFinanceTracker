using FinanceTracker.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Common;

/// <summary>
/// Maps application exceptions to ProblemDetails responses: NotFound → 404,
/// ForbiddenAccess → 403. Other exceptions fall through to the default handler.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            FeatureDisabledException => (StatusCodes.Status503ServiceUnavailable, "Feature Disabled"),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            return false; // not ours — let the default pipeline handle it
        }

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
        };

        // Expose a machine-readable code so the SPA can disable the related
        // controls (rather than just showing a generic error).
        if (exception is FeatureDisabledException disabled)
        {
            problemDetails.Extensions["code"] = disabled.Code;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
        });
    }
}
