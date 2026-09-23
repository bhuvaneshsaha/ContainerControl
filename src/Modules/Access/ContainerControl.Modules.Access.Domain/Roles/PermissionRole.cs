using ContainerControl.Modules.Access.Domain.Permissions;

namespace ContainerControl.Modules.Access.Domain.Roles;

public sealed class PermissionRole
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<PermissionRolePermission> Permissions { get; set; } = [];

    public List<UserPermissionRole> Users { get; set; } = [];
}

public sealed class PermissionRolePermission
{
    public Guid RoleId { get; set; }

    public string PermissionCode { get; set; } = string.Empty;

    public PermissionRole? Role { get; set; }

    public Permission? Permission { get; set; }
}

public sealed class UserPermissionRole
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public PermissionRole? Role { get; set; }
}
