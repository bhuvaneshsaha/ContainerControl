namespace ContainerControl.Modules.Access.Domain.BreakGlass;

/// <summary>
/// A catalog permission granted to one user until <see cref="ExpiresAtUtc"/>.
/// The permission API evaluates it. The grant does not open a shell or the Docker socket.
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
