using ContainerControl.Modules.Access.Infrastructure.Auditing;

namespace ContainerControl.Modules.Access.Infrastructure.Http;

public sealed record CsrfTokenResponse(string? Token);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record CreateUserRequest(string? Email, string? Password, string? DisplayName);

public sealed record CreateUserResponse(Guid Id);

public sealed record UserSummary(Guid Id, string Email, string DisplayName, bool Disabled);

public sealed record UserListResponse(IReadOnlyList<UserSummary> Users);

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

public sealed record AuditListResponse(IReadOnlyList<AuditRow> Entries);

public sealed record BreakGlassRequest(Guid? UserId, string? PermissionCode, int? Minutes);

public sealed record BreakGlassResponse(Guid Id, Guid UserId, string PermissionCode, DateTimeOffset ExpiresAtUtc);

public sealed record BreakGlassListResponse(IReadOnlyList<BreakGlassResponse> Grants, IReadOnlyList<PermissionCatalogItem> Permissions);
