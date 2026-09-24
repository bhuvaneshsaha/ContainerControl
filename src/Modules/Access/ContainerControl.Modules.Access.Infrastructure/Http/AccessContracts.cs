namespace ContainerControl.Modules.Access.Infrastructure.Http;

public sealed record CsrfTokenResponse(string? Token);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record CreateUserRequest(string? Email, string? Password, string? DisplayName);

public sealed record CreateUserResponse(Guid Id);

public sealed record CurrentUserPermissionsResponse(IReadOnlyList<string> Permissions);

public sealed record SessionResponse(bool SignedIn, IReadOnlyList<string> Permissions);

public sealed record PermissionCatalogItem(string Code, string DisplayName, string Module, string Description);

public sealed record PermissionCatalogResponse(IReadOnlyList<PermissionCatalogItem> Permissions);

public sealed record RoleResponse(Guid Id, string Name, string? Description, IReadOnlyList<string> PermissionCodes);

public sealed record RoleListResponse(IReadOnlyList<RoleResponse> Roles);

public sealed record SaveRoleRequest(string? Name, string? Description, IReadOnlyList<string>? PermissionCodes);

public sealed record AssignUserRolesRequest(IReadOnlyList<Guid>? RoleIds);

public sealed record TeamResponse(Guid Id, string Name);

public sealed record TeamListResponse(IReadOnlyList<TeamResponse> Teams);

public sealed record CreateTeamRequest(string? Name);

public sealed record AddTeamMemberRequest(Guid? UserId);

public sealed record IssueTokenRequest(string? Name);

public sealed record IssueTokenResponse(Guid Id, string Token);
