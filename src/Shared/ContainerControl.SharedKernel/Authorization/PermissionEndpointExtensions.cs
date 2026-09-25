using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace ContainerControl.SharedKernel.Authorization;

public static class PermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return builder.RequireAuthorization(PermissionPolicy.Prefix + permission);
    }

    public static RouteHandlerBuilder RequireAnyPermission(this RouteHandlerBuilder builder, params string[] permissions)
    {
        var requirement = new AnyPermissionRequirement(permissions);
        return builder.RequireAuthorization(policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(requirement);
        });
    }
}
