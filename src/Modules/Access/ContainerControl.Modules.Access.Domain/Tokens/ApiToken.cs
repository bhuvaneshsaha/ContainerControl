namespace ContainerControl.Modules.Access.Domain.Tokens;

/// <summary>
/// Placeholder for a hashed CI token. Issuance is a later slice.
/// </summary>
public sealed class ApiToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? TokenHash { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
