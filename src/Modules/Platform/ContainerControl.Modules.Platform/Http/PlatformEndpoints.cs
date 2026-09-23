using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Platform.Http;

public sealed record HostSummary(Guid Id, string Name);

public sealed record HostListResponse(IReadOnlyList<HostSummary> Hosts);

public static class PlatformPermissions
{
    public const string HostsManage = "platform.hosts.manage";
}

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/platform/hosts", () => Results.Ok(new HostListResponse([])))
            .RequirePermission(PlatformPermissions.HostsManage)
            .WithName("ListDockerHosts")
            .WithTags("Platform")
            .WithSummary("Lists Docker hosts. Registration is not implemented in this slice, so the list is empty.")
            .Produces<HostListResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        return endpoints;
    }
}
