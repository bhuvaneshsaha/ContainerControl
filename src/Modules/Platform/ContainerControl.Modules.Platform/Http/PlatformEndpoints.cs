using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Platform.Http;

public sealed record HostSummary(Guid Id, string Name, string Endpoint, string? EngineVersion, DateTimeOffset? LastPingAtUtc);

public sealed record HostListResponse(IReadOnlyList<HostSummary> Hosts);

public sealed record RegisterHostRequest(string? Name, string? Endpoint);

public sealed record RegisterHostResponse(Guid Id);

public sealed record PingHostResponse(string EngineVersion);

public static class PlatformPermissions
{
    public const string HostsManage = "platform.hosts.manage";
}

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/platform/hosts", async (HostRegistry hosts, CancellationToken cancellationToken) =>
            {
                var list = await hosts.ListAsync(cancellationToken);
                return Results.Ok(new HostListResponse(list.Select(ToSummary).ToArray()));
            })
            .RequirePermission(PlatformPermissions.HostsManage)
            .WithName("ListDockerHosts")
            .WithTags("Platform")
            .Produces<HostListResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/platform/hosts/choices", async (HostRegistry hosts, CancellationToken cancellationToken) =>
            {
                var list = await hosts.ListAsync(cancellationToken);
                return Results.Ok(new HostListResponse(list.Select(host => new HostSummary(host.Id, host.Name, string.Empty, null, null)).ToArray()));
            })
            .RequirePermission("apps.write")
            .WithName("ListDockerHostChoices")
            .WithTags("Platform")
            .WithSummary("Returns host ids and names for application placement. The Engine endpoint is omitted.");

        endpoints.MapPost("/platform/hosts", async (
                RegisterHostRequest? request,
                HostRegistry hosts,
                CancellationToken cancellationToken) =>
            {
                var result = await hosts.RegisterAsync(request?.Name ?? string.Empty, request?.Endpoint ?? string.Empty, cancellationToken);
                if (!result.Ok || result.Id is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["host"] = [result.Error ?? "The host was not registered."]
                    });
                }

                return Results.Created($"/platform/hosts/{result.Id}", new RegisterHostResponse(result.Id.Value));
            })
            .RequirePermission(PlatformPermissions.HostsManage)
            .WithName("RegisterDockerHost")
            .WithTags("Platform")
            .Produces<RegisterHostResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        endpoints.MapPost("/platform/hosts/{hostId:guid}/ping", async (
                Guid hostId,
                HostRegistry hosts,
                CancellationToken cancellationToken) =>
            {
                var result = await hosts.PingAsync(hostId, cancellationToken);
                if (result.Error == "not-found")
                {
                    return Results.NotFound();
                }

                if (!result.Ok || result.Version is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: result.Error);
                }

                return Results.Ok(new PingHostResponse(result.Version));
            })
            .RequirePermission(PlatformPermissions.HostsManage)
            .WithName("PingDockerHost")
            .WithTags("Platform")
            .WithSummary("Reads the Engine version. This call does not create a container.")
            .Produces<PingHostResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static HostSummary ToSummary(DockerHost host) =>
        new(host.Id, host.Name, host.Endpoint, host.EngineVersion, host.LastPingAtUtc);
}
