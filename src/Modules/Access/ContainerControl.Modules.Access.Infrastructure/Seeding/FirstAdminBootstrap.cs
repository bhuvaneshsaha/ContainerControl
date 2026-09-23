using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Roles;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Access.Infrastructure.Seeding;

/// <summary>
/// Creates the first administrator when the database has no users.
/// Values come from host environment variables mapped onto BootstrapAdmin configuration.
/// </summary>
public sealed class FirstAdminBootstrap
{
    public const string EmailVariable = "CONTAINERCONTROL_ADMIN_EMAIL";
    public const string PasswordVariable = "CONTAINERCONTROL_ADMIN_PASSWORD";
    public const string DisplayNameVariable = "CONTAINERCONTROL_ADMIN_DISPLAY_NAME";

    private readonly AccessDbContext _db;
    private readonly UserManager<User> _users;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ILogger<FirstAdminBootstrap> _logger;

    public FirstAdminBootstrap(
        AccessDbContext db,
        UserManager<User> users,
        IConfiguration configuration,
        IClock clock,
        IAuditSink audit,
        ILogger<FirstAdminBootstrap> logger)
    {
        _db = db;
        _users = users;
        _configuration = configuration;
        _clock = clock;
        _audit = audit;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (await _users.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var email = _configuration["BootstrapAdmin:Email"];
        var password = _configuration["BootstrapAdmin:Password"];
        var displayName = _configuration["BootstrapAdmin:DisplayName"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "The database has no users. Set {EmailVariable} and {PasswordVariable} to create the first administrator. No account was created.",
                EmailVariable,
                PasswordVariable);
            return;
        }

        var permissionCodes = await _db.Permissions
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        if (permissionCodes.Count == 0)
        {
            permissionCodes = PermissionCatalog.All.Select(permission => permission.Code).ToList();
        }

        var role = new PermissionRole
        {
            Id = Guid.NewGuid(),
            Name = "Platform administrator",
            Description = "Initial administrator role created because the database had no users.",
            CreatedAtUtc = _clock.UtcNow
        };
        foreach (var code in permissionCodes.Distinct(StringComparer.Ordinal))
        {
            role.Permissions.Add(new PermissionRolePermission
            {
                RoleId = role.Id,
                PermissionCode = code
            });
        }

        _db.PermissionRoles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Administrator" : displayName.Trim(),
            LockoutEnabled = true,
            CreatedAtUtc = _clock.UtcNow
        };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            _logger.LogError("The first administrator was not created. {Errors}", errors);
            throw new InvalidOperationException("The first administrator was not created. Check the password policy and the administrator environment variables.");
        }

        _db.UserPermissionRoles.Add(new UserPermissionRole { UserId = user.Id, RoleId = role.Id });
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            new AuditRecord("access.user.created", "user", user.Id.ToString(), user.Id),
            cancellationToken);
        _logger.LogInformation("Created the first administrator account for {Email}.", user.Email);
    }
}
