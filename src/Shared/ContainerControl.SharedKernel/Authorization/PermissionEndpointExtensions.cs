using Microsoft.AspNetCore.Builder;

namespace ContainerControl.SharedKernel.Authorization;

public static class PermissionEndpointExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return builder.RequireAuthorization(PermissionPolicy.Prefix + permission);
    }
}
