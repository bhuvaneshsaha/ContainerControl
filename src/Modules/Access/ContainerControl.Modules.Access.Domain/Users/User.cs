using Microsoft.AspNetCore.Identity;

namespace ContainerControl.Modules.Access.Domain.Users;

public sealed class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
