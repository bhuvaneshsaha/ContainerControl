using ContainerControl.SharedKernel.Authorization;
using ContainerControl.SharedKernel.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.SharedKernel.DependencyInjection;

public static class SharedKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, AnyPermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        return services;
    }
}
