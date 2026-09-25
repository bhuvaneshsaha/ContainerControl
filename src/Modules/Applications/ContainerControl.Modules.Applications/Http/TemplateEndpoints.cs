using System.Security.Claims;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Applications.Templates;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Applications.Http;

public sealed record TemplateResponse(Guid Id, string Name, string Description, string ComposeYaml);

public sealed record TemplateListResponse(IReadOnlyList<TemplateResponse> Templates);

public sealed record PublishTemplateRequest(string? Name, string? Description, string? ComposeYaml);

public sealed record CreateAppFromTemplateRequest(
    Guid? TemplateId,
    Guid? TeamId,
    Guid? HostId,
    string? Name,
    string? Environment,
    int? InternalPort,
    string? Hostname,
    bool Exposed,
    bool RequiresApproval,
    bool? AllowDatabaseImages = null);

public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/templates", async (TemplateAdmin templates, CancellationToken cancellationToken) =>
            {
                var list = await templates.ListAsync(cancellationToken);
                return Results.Ok(new TemplateListResponse(list.Select(ToResponse).ToArray()));
            })
            .RequireAnyPermission(
                PermissionCatalog.AppsRead,
                PermissionCatalog.AppsWrite,
                PermissionCatalog.AppsTemplatesManage)
            .WithName("ListTemplates")
            .WithTags("Templates")
            .Produces<TemplateListResponse>();

        endpoints.MapPost("/templates", async (PublishTemplateRequest? request, TemplateAdmin templates, CancellationToken cancellationToken) =>
            {
                var result = await templates.PublishAsync(
                    request?.Name,
                    request?.Description,
                    request?.ComposeYaml,
                    cancellationToken);
                if (!result.Ok || result.Id is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["template"] = result.Errors.Count == 0
                            ? ["The template was not published."]
                            : result.Errors.ToArray()
                    });
                }

                return Results.Created($"/templates/{result.Id}", new { id = result.Id });
            })
            .RequirePermission(PermissionCatalog.AppsTemplatesManage)
            .WithName("PublishTemplate")
            .WithTags("Templates")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        endpoints.MapDelete("/templates/{templateId:guid}", async (Guid templateId, TemplateAdmin templates, CancellationToken cancellationToken) =>
            {
                var removed = await templates.RemoveAsync(templateId, cancellationToken);
                return removed ? Results.NoContent() : Results.NotFound();
            })
            .RequirePermission(PermissionCatalog.AppsTemplatesManage)
            .WithName("RemoveTemplate")
            .WithTags("Templates")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/apps/from-template", async (
                CreateAppFromTemplateRequest? request,
                ClaimsPrincipal principal,
                TemplateAdmin templates,
                CancellationToken cancellationToken) =>
            {
                if (request?.TemplateId is null || request.TemplateId == Guid.Empty || request.TeamId is null || request.HostId is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["app"] = ["Enter a template, a team, and a Docker host."]
                    });
                }

                var allowDatabaseImages = request.AllowDatabaseImages ?? false;
                if (!DatabaseImageOverride.MayEnable(allowDatabaseImages, currentlyAllowed: false, CallerMayManageSettings(principal)))
                {
                    return Results.Json(
                        new { title = "Allowing database images needs Manage platform settings." },
                        statusCode: StatusCodes.Status403Forbidden);
                }

                var result = await templates.CreateApplicationAsync(
                    request.TemplateId.Value,
                    request.TeamId.Value,
                    request.HostId.Value,
                    request.Name ?? string.Empty,
                    request.Environment ?? string.Empty,
                    request.InternalPort,
                    request.Hostname,
                    request.Exposed,
                    request.RequiresApproval,
                    allowDatabaseImages,
                    cancellationToken);
                if (result.Error == "not-found")
                {
                    return Results.NotFound();
                }

                if (!result.Ok || result.Id is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["app"] = [result.Error ?? "The application was not created."]
                    });
                }

                return Results.Created($"/apps/{result.Id}", new { id = result.Id });
            })
            .RequirePermission(PermissionCatalog.AppsWrite)
            .WithName("CreateAppFromTemplate")
            .WithTags("Templates")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static bool CallerMayManageSettings(ClaimsPrincipal principal) =>
        principal.HasClaim(PermissionPolicy.ClaimType, PermissionCatalog.PlatformSettingsManage);

    private static TemplateResponse ToResponse(AppTemplate template) =>
        new(template.Id, template.Name, template.Description, template.ComposeYaml);
}
