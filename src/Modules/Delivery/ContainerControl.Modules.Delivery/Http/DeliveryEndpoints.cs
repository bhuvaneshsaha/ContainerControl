using System.Security.Claims;
using ContainerControl.Modules.Access.Application.Permissions;
using ContainerControl.Modules.Access.Application.Tokens;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ContainerControl.Modules.Delivery.Http;

public sealed record DeploymentResponse(Guid Id, string Status, string? Error, DateTimeOffset CreatedAtUtc);

public sealed record DeploymentListResponse(IReadOnlyList<DeploymentResponse> Deployments);

public sealed record WebhookDeployRequest(Guid? ApplicationId);

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/apps/{appId:guid}/deploy", async (Guid appId, HttpContext http, DeployService deploy, CancellationToken cancellationToken) =>
                ToResult(await deploy.DeployAsync(appId, UserId(http), approved: false, cancellationToken)))
            .RequirePermission(PermissionCatalog.DeployExecute)
            .WithName("DeployApp")
            .WithTags("Delivery");

        endpoints.MapPost("/apps/{appId:guid}/approve", async (Guid appId, HttpContext http, DeployService deploy, CancellationToken cancellationToken) =>
                ToResult(await deploy.DeployAsync(appId, UserId(http), approved: true, cancellationToken)))
            .RequirePermission(PermissionCatalog.DeployApprove)
            .WithName("ApproveDeploy")
            .WithTags("Delivery");

        endpoints.MapPost("/apps/{appId:guid}/rollback", async (Guid appId, DeployService deploy, CancellationToken cancellationToken) =>
                ToResult(await deploy.RollbackAsync(appId, cancellationToken)))
            .RequirePermission(PermissionCatalog.DeployRollback)
            .WithName("RollbackApp")
            .WithTags("Delivery");

        MapPower(endpoints, "start");
        MapPower(endpoints, "stop");
        MapPower(endpoints, "restart");

        endpoints.MapGet("/apps/{appId:guid}/deployments", async (Guid appId, DeployService deploy, CancellationToken cancellationToken) =>
            {
                var history = await deploy.HistoryAsync(appId, cancellationToken);
                return Results.Ok(new DeploymentListResponse(history.Select(item => new DeploymentResponse(item.Id, item.Status, item.Error, item.CreatedAtUtc)).ToArray()));
            })
            .RequirePermission(PermissionCatalog.AppsRead)
            .WithName("ListDeployments")
            .WithTags("Delivery")
            .Produces<DeploymentListResponse>();

        endpoints.MapPost("/delivery/webhook", async (
                HttpRequest request,
                IApiTokenAuthenticator tokens,
                IPermissionReader permissions,
                DeployService deploy,
                CancellationToken cancellationToken) =>
            {
                var header = request.Headers.Authorization.ToString();
                if (!header.StartsWith("Bearer ", StringComparison.Ordinal))
                {
                    return Results.Unauthorized();
                }

                var userId = await tokens.AuthenticateAsync(header["Bearer ".Length..].Trim(), cancellationToken);
                if (userId is null)
                {
                    return Results.Unauthorized();
                }

                var codes = await permissions.GetEffectivePermissionCodesAsync(userId.Value, cancellationToken);
                if (!codes.Contains(PermissionCatalog.DeployExecute, StringComparer.Ordinal))
                {
                    return Results.Json(new { title = "This token cannot deploy." }, statusCode: StatusCodes.Status403Forbidden);
                }

                var body = await request.ReadFromJsonAsync<WebhookDeployRequest>(cancellationToken);
                if (body?.ApplicationId is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["applicationId"] = ["Enter an application id."]
                    });
                }

                return ToResult(await deploy.DeployAsync(body.ApplicationId.Value, userId.Value, approved: false, cancellationToken));
            })
            .AllowAnonymous()
            .WithName("DeployWebhook")
            .WithTags("Delivery");

        return endpoints;
    }

    private static void MapPower(IEndpointRouteBuilder endpoints, string action)
    {
        endpoints.MapPost($"/apps/{{appId:guid}}/{action}", async (Guid appId, DeployService deploy, CancellationToken cancellationToken) =>
                ToResult(await deploy.ChangePowerAsync(appId, action, cancellationToken)))
            .RequirePermission(PermissionCatalog.RuntimeControl)
            .WithName(char.ToUpperInvariant(action[0]) + action[1..] + "App")
            .WithTags("Delivery");
    }

    private static Guid UserId(HttpContext http)
    {
        var value = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private static IResult ToResult(DeployOutcome outcome)
    {
        if (outcome.Succeeded)
        {
            return Results.Ok(new { status = outcome.Status });
        }

        return Results.Problem(statusCode: outcome.StatusCode, title: outcome.Error);
    }
}
