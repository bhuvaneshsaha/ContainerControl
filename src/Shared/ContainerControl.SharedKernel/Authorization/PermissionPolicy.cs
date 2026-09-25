using Microsoft.AspNetCore.Authorization;

namespace ContainerControl.SharedKernel.Authorization;

public static class PermissionPolicy
{
    public const string Prefix = "permission:";

    public const string ClaimType = "permission";
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim(PermissionPolicy.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class AnyPermissionRequirement : IAuthorizationRequirement
{
    public AnyPermissionRequirement(IReadOnlyList<string> permissions)
    {
        if (permissions.Count == 0 || permissions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one permission code is required.", nameof(permissions));
        }

        Permissions = permissions;
    }

    public IReadOnlyList<string> Permissions { get; }
}

public sealed class AnyPermissionAuthorizationHandler : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        if (requirement.Permissions.Any(permission => context.User.HasClaim(PermissionPolicy.ClaimType, permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
