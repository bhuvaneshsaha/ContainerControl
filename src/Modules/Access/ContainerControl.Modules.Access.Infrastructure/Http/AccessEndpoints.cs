using System.Security.Claims;
using ContainerControl.Modules.Access.Application.Permissions;
using ContainerControl.Modules.Access.Application.SignIn;
using ContainerControl.Modules.Access.Application.Users;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Infrastructure.Roles;
using ContainerControl.Modules.Access.Infrastructure.Teams;
using ContainerControl.Modules.Access.Infrastructure.Tokens;
using ContainerControl.SharedKernel.Authorization;
using ContainerControl.SharedKernel.CurrentUser;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace ContainerControl.Modules.Access.Infrastructure.Http;

public static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/csrf", (HttpContext http, IAntiforgery antiforgery, IHostEnvironment environment) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(http);
                if (!string.IsNullOrEmpty(tokens.RequestToken))
                {
                    var secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
                    http.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken, new CookieOptions
                    {
                        HttpOnly = false,
                        Secure = secure,
                        SameSite = SameSiteMode.Lax,
                        Path = "/"
                    });
                    http.Response.Headers["X-XSRF-TOKEN"] = tokens.RequestToken;
                }

                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("IssueCsrfToken")
            .WithTags("Access")
            .WithSummary("Issues the cookie anti-forgery token used by the browser.");

        endpoints.MapGet("/auth/session", (ClaimsPrincipal principal) =>
            {
                if (principal.Identity?.IsAuthenticated != true)
                {
                    return Results.Ok(new SessionResponse(false, []));
                }

                return Results.Ok(new SessionResponse(true, ReadPermissions(principal)));
            })
            .AllowAnonymous()
            .WithName("GetSession")
            .WithTags("Access")
            .WithSummary("Returns whether the browser cookie is signed in, without challenging an anonymous caller.")
            .Produces<SessionResponse>();

        endpoints.MapPost("/auth/login", async (
                LoginRequest? request,
                SignInService signIn,
                CancellationToken cancellationToken) =>
            {
                if (request is null
                    || string.IsNullOrWhiteSpace(request.Email)
                    || string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["credentials"] = ["Enter an email and password."]
                    });
                }

                var status = await signIn.SignInAsync(request.Email.Trim(), request.Password, cancellationToken);
                return status == SignInStatus.Succeeded
                    ? Results.NoContent()
                    : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in was rejected.");
            })
            .AllowAnonymous()
            .WithName("SignIn")
            .WithTags("Access")
            .WithSummary("Signs in with email and password and sets the authentication cookie.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        endpoints.MapPost("/auth/logout", async (
                SignInService signIn,
                ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                await signIn.SignOutAsync(currentUser.UserId, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("SignOut")
            .WithTags("Access")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapGet("/me/permissions", (ClaimsPrincipal principal) =>
                Results.Ok(new CurrentUserPermissionsResponse(ReadPermissions(principal))))
            .RequireAuthorization()
            .WithName("GetMyPermissions")
            .WithTags("Access")
            .WithSummary("Returns the permission codes on the signed-in cookie.")
            .Produces<CurrentUserPermissionsResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapGet("/permissions", async (IPermissionReader reader, CancellationToken cancellationToken) =>
            {
                var catalog = await reader.GetCatalogAsync(cancellationToken);
                var items = catalog
                    .Select(permission => new PermissionCatalogItem(
                        permission.Code,
                        permission.DisplayName,
                        permission.Module,
                        permission.Description))
                    .ToArray();
                return Results.Ok(new PermissionCatalogResponse(items));
            })
            .RequirePermission(PermissionCatalog.AccessRolesManage)
            .WithName("GetPermissionCatalog")
            .WithTags("Access")
            .Produces<PermissionCatalogResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/access/users", async (
                CreateUserRequest? request,
                UserAdminService users,
                CancellationToken cancellationToken) =>
            {
                if (request is null
                    || string.IsNullOrWhiteSpace(request.Email)
                    || string.IsNullOrWhiteSpace(request.Password)
                    || string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["user"] = ["Enter an email, a password, and a display name."]
                    });
                }

                var result = await users.CreateAsync(
                    request.Email.Trim(),
                    request.Password,
                    request.DisplayName,
                    cancellationToken);
                if (result.NotFound)
                {
                    return Results.NotFound();
                }

                if (!result.Succeeded || result.UserId is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["user"] = result.Errors.ToArray()
                    });
                }

                return Results.Created($"/access/users/{result.UserId}", new CreateUserResponse(result.UserId.Value));
            })
            .RequirePermission(PermissionCatalog.AccessUsersManage)
            .WithName("CreateUser")
            .WithTags("Access")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        endpoints.MapPost("/access/users/{userId:guid}/disable", async (
                Guid userId,
                UserAdminService users,
                CancellationToken cancellationToken) =>
            {
                var result = await users.DisableAsync(userId, cancellationToken);
                if (result.NotFound)
                {
                    return Results.NotFound();
                }

                if (!result.Succeeded)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["user"] = result.Errors.ToArray()
                    });
                }

                return Results.NoContent();
            })
            .RequirePermission(PermissionCatalog.AccessUsersManage)
            .WithName("DisableUser")
            .WithTags("Access")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapPut("/access/users/{userId:guid}/roles", async (
                Guid userId,
                AssignUserRolesRequest? request,
                RoleAdminService roles,
                CancellationToken cancellationToken) =>
            {
                var result = await roles.AssignUserRolesAsync(
                    userId,
                    request?.RoleIds ?? [],
                    cancellationToken);
                if (result.NotFound)
                {
                    return Results.NotFound();
                }

                if (!result.Succeeded)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["roles"] = result.Errors.ToArray()
                    });
                }

                return Results.NoContent();
            })
            .RequirePermission(PermissionCatalog.AccessUsersManage)
            .WithName("AssignUserRoles")
            .WithTags("Access")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/access/roles", async (RoleAdminService roles, CancellationToken cancellationToken) =>
            {
                var list = await roles.ListAsync(cancellationToken);
                return Results.Ok(new RoleListResponse(list.Select(ToResponse).ToArray()));
            })
            .RequirePermission(PermissionCatalog.AccessRolesManage)
            .WithName("ListRoles")
            .WithTags("Access")
            .Produces<RoleListResponse>();

        endpoints.MapPost("/access/roles", async (
                SaveRoleRequest? request,
                RoleAdminService roles,
                CancellationToken cancellationToken) =>
            {
                var result = await roles.CreateAsync(
                    request?.Name ?? string.Empty,
                    request?.Description,
                    request?.PermissionCodes ?? [],
                    cancellationToken);
                return ToRoleResult(result, created: true);
            })
            .RequirePermission(PermissionCatalog.AccessRolesManage)
            .WithName("CreateRole")
            .WithTags("Access")
            .Produces<RoleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        endpoints.MapPut("/access/roles/{roleId:guid}", async (
                Guid roleId,
                SaveRoleRequest? request,
                RoleAdminService roles,
                CancellationToken cancellationToken) =>
            {
                var result = await roles.UpdateAsync(
                    roleId,
                    request?.Name ?? string.Empty,
                    request?.Description,
                    request?.PermissionCodes ?? [],
                    cancellationToken);
                return ToRoleResult(result, created: false);
            })
            .RequirePermission(PermissionCatalog.AccessRolesManage)
            .WithName("UpdateRole")
            .WithTags("Access")
            .Produces<RoleResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        endpoints.MapGet("/access/teams", async (ClaimsPrincipal principal, TeamDirectory teams, CancellationToken cancellationToken) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var all = principal.HasClaim(PermissionPolicy.ClaimType, PermissionCatalog.AccessTeamsManage);
                var list = await teams.ListForUserAsync(userId, all, cancellationToken);
                return Results.Ok(new TeamListResponse(list.Select(team => new TeamResponse(team.Id, team.Name)).ToArray()));
            })
            .RequireAuthorization()
            .WithName("ListTeams")
            .WithTags("Access")
            .Produces<TeamListResponse>();

        endpoints.MapPost("/access/teams", async (CreateTeamRequest? request, TeamDirectory teams, CancellationToken cancellationToken) =>
            {
                var result = await teams.CreateAsync(request?.Name ?? string.Empty, cancellationToken);
                if (!result.Ok || result.Id is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["team"] = [result.Error ?? "The team was not created."] });
                }

                return Results.Created($"/access/teams/{result.Id}", new TeamResponse(result.Id.Value, request!.Name!.Trim()));
            })
            .RequirePermission(PermissionCatalog.AccessTeamsManage)
            .WithName("CreateTeam")
            .WithTags("Access")
            .Produces<TeamResponse>(StatusCodes.Status201Created);

        endpoints.MapPost("/access/teams/{teamId:guid}/members", async (
                Guid teamId,
                AddTeamMemberRequest? request,
                TeamDirectory teams,
                CancellationToken cancellationToken) =>
            {
                if (request?.UserId is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["member"] = ["Enter a user id."] });
                }

                var added = await teams.AddMemberAsync(teamId, request.UserId.Value, cancellationToken);
                return added ? Results.NoContent() : Results.NotFound();
            })
            .RequirePermission(PermissionCatalog.AccessTeamsManage)
            .WithName("AddTeamMember")
            .WithTags("Access");

        endpoints.MapPost("/access/tokens", async (
                ClaimsPrincipal principal,
                IssueTokenRequest? request,
                TokenAdminService tokens,
                CancellationToken cancellationToken) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await tokens.IssueAsync(userId, request?.Name ?? string.Empty, cancellationToken);
                if (!result.Ok || result.Id is null || result.Plaintext is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["token"] = [result.Error ?? "The token was not issued."] });
                }

                return Results.Created($"/access/tokens/{result.Id}", new IssueTokenResponse(result.Id.Value, result.Plaintext));
            })
            .RequirePermission(PermissionCatalog.AccessTokensManage)
            .WithName("IssueApiToken")
            .WithTags("Access")
            .Produces<IssueTokenResponse>(StatusCodes.Status201Created);

        endpoints.MapDelete("/access/roles/{roleId:guid}", async (
                Guid roleId,
                RoleAdminService roles,
                CancellationToken cancellationToken) =>
            {
                var deleted = await roles.DeleteAsync(roleId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .RequirePermission(PermissionCatalog.AccessRolesManage)
            .WithName("DeleteRole")
            .WithTags("Access")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static string[] ReadPermissions(ClaimsPrincipal principal) =>
        principal
            .FindAll(PermissionPolicy.ClaimType)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

    private static IResult ToRoleResult(RoleWriteResult result, bool created)
    {
        if (result.NotFound)
        {
            return Results.NotFound();
        }

        if (!result.Succeeded || result.Role is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = result.Errors.ToArray()
            });
        }

        var body = ToResponse(result.Role);
        return created
            ? Results.Created($"/access/roles/{body.Id}", body)
            : Results.Ok(body);
    }

    private static RoleResponse ToResponse(RoleDetails role) =>
        new(role.Id, role.Name, role.Description, role.PermissionCodes);
}
