using ContainerControl.Modules.Access.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Access.Infrastructure.Http;

public static class DisabledUserGate
{
    public static IApplicationBuilder UseDisabledUserGate(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var users = context.RequestServices.GetRequiredService<UserManager<User>>();
                var user = await users.GetUserAsync(context.User);
                if (user is null || user.IsDisabled)
                {
                    var signIn = context.RequestServices.GetRequiredService<SignInManager<User>>();
                    await signIn.SignOutAsync();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await next();
        });
    }
}
