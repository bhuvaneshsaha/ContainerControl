using System.Diagnostics;
using ContainerControl.SharedKernel.Correlation;
using Serilog.Context;

namespace ContainerControl.Host.Hosting;

public static class CorrelationMiddleware
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var incoming = context.Request.Headers[Correlation.HeaderName].ToString();
            var correlationId = string.IsNullOrWhiteSpace(incoming)
                ? Guid.NewGuid().ToString("n")
                : incoming.Trim();
            if (correlationId.Length > 128)
            {
                correlationId = correlationId[..128];
            }

            context.Items[Correlation.ItemKey] = correlationId;
            context.Response.Headers[Correlation.HeaderName] = correlationId;
            Activity.Current?.SetTag("correlation.id", correlationId);
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });
    }
}
