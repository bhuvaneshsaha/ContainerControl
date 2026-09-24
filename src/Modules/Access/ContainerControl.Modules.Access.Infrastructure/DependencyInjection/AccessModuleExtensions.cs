using ContainerControl.Modules.Access.Application.Permissions;
using ContainerControl.Modules.Access.Application.SignIn;
using ContainerControl.Modules.Access.Application.Users;
using ContainerControl.Modules.Access.Domain;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.Modules.Access.Infrastructure.Auditing;
using ContainerControl.Modules.Access.Infrastructure.BreakGlass;
using ContainerControl.Modules.Access.Infrastructure.Identity;
using ContainerControl.Modules.Access.Infrastructure.Permissions;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.Modules.Access.Infrastructure.Roles;
using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Access.Application.Tokens;
using ContainerControl.Modules.Access.Infrastructure.Tokens;
using ContainerControl.Modules.Access.Infrastructure.Seeding;
using ContainerControl.Modules.Access.Infrastructure.Teams;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Access.Infrastructure.DependencyInjection;

public static class AccessModuleExtensions
{
    public static IServiceCollection AddAccessModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool secureCookies)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' is not configured.");
        }

        var securePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "ContainerControl.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "ContainerControl.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<AppendOnlyAuditInterceptor>();
        services.AddDbContext<AccessDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AccessSchema.Name);
                npgsql.MigrationsAssembly(typeof(AccessDbContext).Assembly.GetName().Name);
            });
            options.AddInterceptors(provider.GetRequiredService<AppendOnlyAuditInterceptor>());
        });

        services
            .AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<AccessDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<PermissionClaimsPrincipalFactory>();

        services.AddScoped<TeamDirectory>();
        services.AddScoped<ITeamDirectory>(provider => provider.GetRequiredService<TeamDirectory>());
        services.AddScoped<IPermissionReader, PermissionReader>();
        services.AddScoped<IAuditSink, EfAuditSink>();
        services.AddScoped<AuditQuery>();
        services.AddScoped<BreakGlassAdmin>();
        services.AddScoped<IAuthorizationHandler, BreakGlassAuthorizationHandler>();
        services.AddScoped<SignInService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<RoleAdminService>();
        services.AddScoped<TokenAdminService>();
        services.AddScoped<IApiTokenAuthenticator>(provider => provider.GetRequiredService<TokenAdminService>());
        services.AddScoped<DevelopmentAccessSeeder>();
        services.AddScoped<FirstAdminBootstrap>();
        return services;
    }
}
