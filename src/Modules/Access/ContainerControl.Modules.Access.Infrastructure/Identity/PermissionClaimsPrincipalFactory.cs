using System.Security.Claims;
using ContainerControl.Modules.Access.Application.Permissions;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.SharedKernel.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ContainerControl.Modules.Access.Infrastructure.Identity;

public sealed class PermissionClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
{
    private readonly IPermissionReader _permissions;

    public PermissionClaimsPrincipalFactory(
        UserManager<User> userManager,
        IOptions<IdentityOptions> options,
        IPermissionReader permissions)
        : base(userManager, options)
    {
        _permissions = permissions;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim("display_name", user.DisplayName));
        }

        var codes = await _permissions.GetAssignedPermissionCodesAsync(user.Id, CancellationToken.None);
        foreach (var code in codes)
        {
            identity.AddClaim(new Claim(PermissionPolicy.ClaimType, code));
        }

        return identity;
    }
}
