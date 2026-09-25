using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.SharedKernel.Authorization;
using Docker.DotNet;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Edge.Http;

public sealed record AllowedDomainResponse(Guid Id, string Name);

public sealed record AllowedDomainListResponse(IReadOnlyList<AllowedDomainResponse> Domains);

public sealed record SaveDomainRequest(string? Name);

public static class EdgePermissions
{
    public const string DnsManage = "edge.dns.manage";
}

public static class EdgeEndpoints
{
    public static IEndpointRouteBuilder MapEdgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/edge/domains", async (EdgeGateway edge, CancellationToken cancellationToken) =>
            {
                var list = await edge.ListAsync(cancellationToken);
                return Results.Ok(new AllowedDomainListResponse(list.Select(domain => new AllowedDomainResponse(domain.Id, domain.Name)).ToArray()));
            })
            .RequirePermission(EdgePermissions.DnsManage)
            .WithName("ListAllowedDomains")
            .WithTags("Edge")
            .Produces<AllowedDomainListResponse>();

        endpoints.MapPost("/edge/domains", async (SaveDomainRequest? request, EdgeGateway edge, CancellationToken cancellationToken) =>
            {
                var result = await edge.AddAsync(request?.Name ?? string.Empty, cancellationToken);
                if (!result.Ok)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["domain"] = [result.Error ?? "The domain was not saved."] });
                }

                return Results.NoContent();
            })
            .RequirePermission(EdgePermissions.DnsManage)
            .WithName("SaveAllowedDomain")
            .WithTags("Edge");

        endpoints.MapPost("/platform/hosts/{hostId:guid}/prepare", async (
                Guid hostId,
                ContainerControl.Modules.Platform.Hosts.IDockerHostLookup hosts,
                EdgeGateway edge,
                CancellationToken cancellationToken) =>
            {
                var host = await hosts.FindAsync(hostId, cancellationToken);
                if (host is null)
                {
                    return Results.NotFound();
                }

                try
                {
                    await edge.EnsureEdgeAsync(host.Endpoint, cancellationToken);
                }
                catch (Exception exception) when (exception is DockerEngineException or DockerApiException or HttpRequestException or IOException)
                {
                    var title = exception is DockerEngineException engine && EngineTls.IsOperatorMessage(engine.Message)
                        ? engine.Message
                        : "The Docker host could not be prepared.";
                    return Results.Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: title);
                }

                return Results.NoContent();
            })
            .RequirePermission(ContainerControl.Modules.Platform.Http.PlatformPermissions.HostsManage)
            .WithName("PrepareDockerHost")
            .WithTags("Edge")
            .WithSummary("Creates the edge network and the Traefik container when they are missing.");

        return endpoints;
    }
}
