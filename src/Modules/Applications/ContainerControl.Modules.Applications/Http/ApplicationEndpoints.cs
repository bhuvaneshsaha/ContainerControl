using System.Security.Claims;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Applications.Http;

public sealed record AppResponse(
    Guid Id,
    Guid TeamId,
    Guid HostId,
    string Name,
    string Environment,
    string? Image,
    string? ComposeYaml,
    int? InternalPort,
    string? Hostname,
    bool Exposed,
    string Status);

public sealed record AppListResponse(IReadOnlyList<AppResponse> Apps);

public sealed record CreateAppRequest(
    Guid? TeamId,
    Guid? HostId,
    string? Name,
    string? Environment,
    string? Image,
    IReadOnlyList<string>? Command,
    string? ComposeYaml,
    int? InternalPort,
    string? Hostname,
    bool Exposed);

public sealed record SecretResponse(Guid Id, string Name, string Environment, string InjectionMode, string Path);

public sealed record SecretListResponse(IReadOnlyList<SecretResponse> Secrets);

public sealed record SaveSecretRequest(Guid? TeamId, string? Environment, string? Name, string? InjectionMode, string? Value);

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/apps", async (WorkloadAdmin apps, CancellationToken cancellationToken) =>
            {
                var list = await apps.ListForActorAsync(cancellationToken);
                return Results.Ok(new AppListResponse(list.Select(ToResponse).ToArray()));
            })
            .RequirePermission(PermissionCatalog.AppsRead)
            .WithName("ListApps")
            .WithTags("Applications")
            .Produces<AppListResponse>();

        endpoints.MapGet("/apps/{appId:guid}", async (Guid appId, WorkloadAdmin apps, CancellationToken cancellationToken) =>
            {
                var app = await apps.FindEntityAsync(appId, cancellationToken);
                return app is null ? Results.NotFound() : Results.Ok(ToResponse(app));
            })
            .RequirePermission(PermissionCatalog.AppsRead)
            .WithName("GetApp")
            .WithTags("Applications")
            .Produces<AppResponse>();

        endpoints.MapPost("/apps", async (CreateAppRequest? request, WorkloadAdmin apps, CancellationToken cancellationToken) =>
            {
                if (request?.TeamId is null || request.HostId is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["app"] = ["Enter a team and a Docker host."] });
                }

                var result = await apps.CreateAsync(
                    request.TeamId.Value,
                    request.HostId.Value,
                    request.Name ?? string.Empty,
                    request.Environment ?? string.Empty,
                    request.Image,
                    request.Command,
                    request.ComposeYaml,
                    request.InternalPort,
                    request.Hostname,
                    request.Exposed,
                    cancellationToken);
                if (!result.Ok || result.Id is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["app"] = [result.Error ?? "The application was not created."] });
                }

                return Results.Created($"/apps/{result.Id}", new { id = result.Id });
            })
            .RequirePermission(PermissionCatalog.AppsWrite)
            .WithName("CreateApp")
            .WithTags("Applications")
            .Produces(StatusCodes.Status201Created);

        endpoints.MapPut("/apps/{appId:guid}", async (Guid appId, CreateAppRequest? request, WorkloadAdmin apps, CancellationToken cancellationToken) =>
            {
                var result = await apps.UpdateAsync(
                    appId,
                    request?.Image,
                    request?.Command,
                    request?.ComposeYaml,
                    request?.InternalPort,
                    request?.Hostname,
                    request?.Exposed ?? false,
                    cancellationToken);
                if (result.Error == "not-found")
                {
                    return Results.NotFound();
                }

                if (!result.Ok)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["app"] = [result.Error ?? "The application was not updated."] });
                }

                return Results.NoContent();
            })
            .RequirePermission(PermissionCatalog.AppsWrite)
            .WithName("UpdateApp")
            .WithTags("Applications");

        endpoints.MapGet("/secrets", async (Guid teamId, string environment, WorkloadAdmin apps, CancellationToken cancellationToken) =>
            {
                var list = await apps.ListSecretsAsync(teamId, environment, cancellationToken);
                return Results.Ok(new SecretListResponse(list.Select(secret => new SecretResponse(secret.Id, secret.Name, secret.Environment, secret.InjectionMode, secret.Path)).ToArray()));
            })
            .RequirePermission(PermissionCatalog.SecretsRead)
            .WithName("ListSecrets")
            .WithTags("Applications")
            .Produces<SecretListResponse>();

        endpoints.MapPost("/secrets", async (SaveSecretRequest? request, ClaimsPrincipal principal, WorkloadAdmin apps, CancellationToken cancellationToken) =>
            {
                if (request?.Environment == "prod" && !principal.HasClaim(PermissionPolicy.ClaimType, PermissionCatalog.SecretsManageProd))
                {
                    return Results.Forbid();
                }

                var result = await apps.SaveSecretAsync(
                    request?.TeamId ?? Guid.Empty,
                    request?.Environment ?? string.Empty,
                    request?.Name ?? string.Empty,
                    request?.InjectionMode ?? string.Empty,
                    request?.Value ?? string.Empty,
                    cancellationToken);
                if (!result.Ok)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["secret"] = [result.Error ?? "The secret was not saved."] });
                }

                return Results.NoContent();
            })
            .RequirePermission(PermissionCatalog.SecretsManage)
            .WithName("SaveSecret")
            .WithTags("Applications")
            .Produces(StatusCodes.Status204NoContent);

        endpoints.MapDelete("/secrets/{secretId:guid}", async (Guid secretId, ClaimsPrincipal principal, WorkloadAdmin apps, CancellationToken cancellationToken) =>
            {
                var secret = await apps.FindSecretAsync(secretId, cancellationToken);
                if (secret is null)
                {
                    return Results.NotFound();
                }

                if (secret.Environment == "prod" && !principal.HasClaim(PermissionPolicy.ClaimType, PermissionCatalog.SecretsManageProd))
                {
                    return Results.Forbid();
                }

                var deleted = await apps.DeleteSecretAsync(secretId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .RequirePermission(PermissionCatalog.SecretsManage)
            .WithName("DeleteSecret")
            .WithTags("Applications");

        return endpoints;
    }

    private static AppResponse ToResponse(ContainerApp app) =>
        new(app.Id, app.TeamId, app.HostId, app.Name, app.Environment, app.Image, app.ComposeYaml, app.InternalPort, app.Hostname, app.Exposed, app.Status);
}
