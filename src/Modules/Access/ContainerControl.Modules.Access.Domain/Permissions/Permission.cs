namespace ContainerControl.Modules.Access.Domain.Permissions;

public sealed class Permission
{
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed record PermissionDefinition(string Code, string DisplayName, string Module, string Description);
