using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Registries.Connections;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Registries.Http;

public sealed record RegistryResponse(
    Guid Id,
    string Name,
    string Kind,
    string Server,
    string Environment,
    string UsernamePath,
    string PasswordPath,
    DateTimeOffset? EcrTokenExpiresAtUtc);

public sealed record RegistryListResponse(IReadOnlyList<RegistryResponse> Registries);

public sealed record SaveRegistryRequest(
    string? Name,
    string? Kind,
    string? Server,
    string? Environment,
    string? Username,
    string? Password,
    string? AccessKeyId,
    string? SecretAccessKey);

public static class RegistryEndpoints
{
    public static IEndpointRouteBuilder MapRegistryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/registries", async (RegistryAdmin registries, CancellationToken cancellationToken) =>
            {
                var list = await registries.ListAsync(cancellationToken);
                return Results.Ok(new RegistryListResponse(list.Select(ToResponse).ToArray()));
            })
            .RequirePermission(PermissionCatalog.RegistriesRead)
            .WithName("ListRegistries")
            .WithTags("Registries")
            .Produces<RegistryListResponse>();

        endpoints.MapPost("/registries", async (SaveRegistryRequest? request, RegistryAdmin registries, CancellationToken cancellationToken) =>
            {
                var result = await registries.CreateAsync(
                    request?.Name ?? string.Empty,
                    request?.Kind ?? string.Empty,
                    request?.Server ?? string.Empty,
                    request?.Environment ?? string.Empty,
                    request?.Username,
                    request?.Password,
                    request?.AccessKeyId,
                    request?.SecretAccessKey,
                    cancellationToken);
                if (!result.Ok || result.Id is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["registry"] = [result.Error ?? "The registry was not saved."]
                    });
                }

                return Results.Created($"/registries/{result.Id}", new { id = result.Id });
            })
            .RequirePermission(PermissionCatalog.RegistriesManage)
            .WithName("CreateRegistry")
            .WithTags("Registries")
            .Produces(StatusCodes.Status201Created);

        return endpoints;
    }

    private static RegistryResponse ToResponse(RegistryConnection connection) =>
        new(
            connection.Id,
            connection.Name,
            connection.Kind,
            connection.Server,
            connection.Environment,
            connection.UsernamePath,
            connection.PasswordPath,
            connection.EcrTokenExpiresAtUtc);
}
