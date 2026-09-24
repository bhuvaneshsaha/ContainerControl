namespace ContainerControl.Modules.Access.Domain.BreakGlass;

/// <summary>
/// Placeholder for a time-boxed permission grant. Grants are not evaluated in this slice.
/// </summary>
public sealed class BreakGlassGrant
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string PermissionCode { get; set; } = string.Empty;

    public Guid GrantedByUserId { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
