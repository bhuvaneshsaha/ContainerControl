using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Platform.Quotas;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Platform.Http;

public sealed record HostSummary(
    Guid Id,
    string Name,
    string Endpoint,
    string? EngineVersion,
    DateTimeOffset? LastPingAtUtc,
    string? ClientCertRef = null,
    string? ClientKeyRef = null,
    string? CaRef = null);

public sealed record HostListResponse(IReadOnlyList<HostSummary> Hosts);

public sealed record RegisterHostRequest(
    string? Name,
    string? Endpoint,
    string? ClientCertRef,
    string? ClientKeyRef,
    string? CaRef);

public sealed record RegisterHostResponse(Guid Id);

public sealed record PingHostResponse(string EngineVersion);

public sealed record QuotaResponse(Guid TeamId, long CpuMillicores, long MemoryBytes, long StorageBytes);

public sealed record QuotaListResponse(IReadOnlyList<QuotaResponse> Quotas);

public sealed record SaveQuotaRequest(long? CpuMillicores, long? MemoryBytes, long? StorageBytes);

public sealed record CapacityListResponse(IReadOnlyList<CapacityRow> Hosts);

public static class PlatformPermissions
{
    public const string HostsManage = "platform.hosts.manage";

    public const string QuotasManage = "platform.quotas.manage";

    public const string CapacityRead = "platform.capacity.read";
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
                var result = await hosts.RegisterAsync(
                    request?.Name ?? string.Empty,
                    request?.Endpoint ?? string.Empty,
                    cancellationToken,
                    request?.ClientCertRef,
                    request?.ClientKeyRef,
                    request?.CaRef);
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

        MapQuotaEndpoints(endpoints);
        return endpoints;
    }

    private static void MapQuotaEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/platform/quotas", async (QuotaAdmin quotas, CancellationToken cancellationToken) =>
            {
                var list = await quotas.ListAsync(cancellationToken);
                return Results.Ok(new QuotaListResponse(list.Select(quota => new QuotaResponse(
                    quota.TeamId,
                    quota.CpuMillicores,
                    quota.MemoryBytes,
                    quota.StorageBytes)).ToArray()));
            })
            .RequirePermission(PlatformPermissions.QuotasManage)
            .WithName("ListTeamQuotas")
            .WithTags("Platform");

        endpoints.MapPut("/platform/quotas/{teamId:guid}", async (
                Guid teamId,
                SaveQuotaRequest? request,
                QuotaAdmin quotas,
                CancellationToken cancellationToken) =>
            {
                var result = await quotas.SaveAsync(
                    teamId,
                    request?.CpuMillicores ?? 0,
                    request?.MemoryBytes ?? 0,
                    request?.StorageBytes ?? 0,
                    cancellationToken);
                if (!result.Ok)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["quota"] = [result.Error ?? "The quota was not saved."]
                    });
                }

                return Results.NoContent();
            })
            .RequirePermission(PlatformPermissions.QuotasManage)
            .WithName("SaveTeamQuota")
            .WithTags("Platform");

        endpoints.MapGet("/platform/capacity", async (QuotaAdmin quotas, CancellationToken cancellationToken) =>
            Results.Ok(new CapacityListResponse(await quotas.ListCapacityAsync(cancellationToken))))
            .RequirePermission(PlatformPermissions.CapacityRead)
            .WithName("ListHostCapacity")
            .WithTags("Platform");

        endpoints.MapPost("/platform/capacity/{hostId:guid}", async (
                Guid hostId,
                QuotaAdmin quotas,
                CancellationToken cancellationToken) =>
            {
                var result = await quotas.RecordCapacityAsync(hostId, cancellationToken);
                if (result.Error == "not-found")
                {
                    return Results.NotFound();
                }

                if (!result.Ok)
                {
                    return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: result.Error);
                }

                return Results.NoContent();
            })
            .RequirePermission(PlatformPermissions.CapacityRead)
            .WithName("RecordHostCapacity")
            .WithTags("Platform");
    }

    private static HostSummary ToSummary(DockerHost host) =>
        new(host.Id, host.Name, host.Endpoint, host.EngineVersion, host.LastPingAtUtc, host.ClientCertRef, host.ClientKeyRef, host.CaRef);
}
