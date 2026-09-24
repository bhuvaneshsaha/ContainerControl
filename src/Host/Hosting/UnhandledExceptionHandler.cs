using ContainerControl.SharedKernel.Correlation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Host.Hosting;

public sealed class UnhandledExceptionHandler : IExceptionHandler
{
    private readonly ILogger<UnhandledExceptionHandler> _logger;

    public UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.RequestServices.GetRequiredService<ICorrelationContext>().CorrelationId;
        _logger.LogError(exception, "Unhandled exception. CorrelationId {CorrelationId}", correlationId);
        httpContext.Response.StatusCode = exception is DbUpdateConcurrencyException
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;

        var title = httpContext.Response.StatusCode == StatusCodes.Status409Conflict
            ? "The record was changed by someone else."
            : "The request could not be completed.";

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = httpContext.Response.StatusCode,
                Title = title,
                Extensions = { ["correlationId"] = correlationId }
            },
            cancellationToken);
        return true;
    }
}
