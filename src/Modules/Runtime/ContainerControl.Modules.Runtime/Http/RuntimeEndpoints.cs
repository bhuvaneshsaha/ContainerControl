using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Runtime.Inspection;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Runtime.Http;

public sealed record LogTailResponse(string Text);

public sealed record StatsResponse(IReadOnlyList<ServiceStats> Services);

public static class RuntimeEndpoints
{
    public static IEndpointRouteBuilder MapRuntimeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/apps/{appId:guid}/logs", async (Guid appId, RuntimeInspector runtime, CancellationToken cancellationToken) =>
            {
                var text = await runtime.LogsAsync(appId, cancellationToken);
                return text is null ? Results.NotFound() : Results.Ok(new LogTailResponse(text));
            })
            .RequirePermission(PermissionCatalog.RuntimeLogsRead)
            .WithName("ReadAppLogs")
            .WithTags("Runtime")
            .Produces<LogTailResponse>();

        endpoints.MapGet("/apps/{appId:guid}/stats", async (Guid appId, RuntimeInspector runtime, CancellationToken cancellationToken) =>
            {
                var stats = await runtime.StatsAsync(appId, cancellationToken);
                return stats is null ? Results.NotFound() : Results.Ok(new StatsResponse(stats));
            })
            .RequirePermission(PermissionCatalog.RuntimeStatsRead)
            .WithName("ReadAppStats")
            .WithTags("Runtime")
            .Produces<StatsResponse>();

        endpoints.MapHub<LogHub>("/hubs/logs");

        return endpoints;
    }
}
