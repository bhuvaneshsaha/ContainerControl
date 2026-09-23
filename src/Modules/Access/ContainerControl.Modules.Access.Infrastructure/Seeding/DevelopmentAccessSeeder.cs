using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Roles;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.SharedKernel.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Seeding;

/// <summary>
/// Development-only sample users, teams, and roles. Production never calls this.
/// The passwords are local sample data, not production credentials.
/// </summary>
public sealed class DevelopmentAccessSeeder
{
    public const string AdministratorEmail = "admin@localhost";
    public const string AdministratorPassword = "Dev-Admin-Passw0rd!";
    public const string DeveloperEmail = "developer@localhost";
    public const string DeveloperPassword = "Dev-Developer-Passw0rd!";
    public const string AdministratorRoleName = "Platform administrator";
    public const string DeveloperRoleName = "Developer";

    private static readonly string[] DeveloperPermissionCodes =
    [
        PermissionCatalog.AppsRead,
        PermissionCatalog.AppsWrite,
        PermissionCatalog.SecretsRead,
        PermissionCatalog.SecretsManage,
        PermissionCatalog.DeployExecute,
        PermissionCatalog.DeployRollback,
        PermissionCatalog.RegistriesRead,
        PermissionCatalog.RuntimeLogsRead,
        PermissionCatalog.RuntimeStatsRead,
        PermissionCatalog.RuntimeControl
    ];

    private readonly AccessDbContext _db;
    private readonly UserManager<User> _users;
    private readonly IClock _clock;

    public DevelopmentAccessSeeder(AccessDbContext db, UserManager<User> users, IClock clock)
    {
        _db = db;
        _users = users;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var administratorRole = await EnsureRoleAsync(
            AdministratorRoleName,
            "Development sample role with every catalog permission.",
            PermissionCatalog.All.Select(permission => permission.Code).ToArray(),
            cancellationToken);
        var developerRole = await EnsureRoleAsync(
            DeveloperRoleName,
            "Development sample role for application work. It cannot manage Docker hosts.",
            DeveloperPermissionCodes,
            cancellationToken);

        var platformTeam = await EnsureTeamAsync("Platform", cancellationToken);
        var productTeam = await EnsureTeamAsync("Product", cancellationToken);

        var administrator = await EnsureUserAsync(
            AdministratorEmail,
            AdministratorPassword,
            "Sample administrator",
            cancellationToken);
        var developer = await EnsureUserAsync(
            DeveloperEmail,
            DeveloperPassword,
            "Sample developer",
            cancellationToken);

        await EnsureAssignmentAsync(administrator.Id, administratorRole.Id, cancellationToken);
        await EnsureAssignmentAsync(developer.Id, developerRole.Id, cancellationToken);
        await EnsureMembershipAsync(platformTeam.Id, administrator.Id, cancellationToken);
        await EnsureMembershipAsync(productTeam.Id, developer.Id, cancellationToken);
    }

    private async Task<PermissionRole> EnsureRoleAsync(
        string name,
        string description,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await _db.PermissionRoles
            .Include(item => item.Permissions)
            .SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new PermissionRole
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
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
        return role;
    }

    private async Task<Team> EnsureTeamAsync(string name, CancellationToken cancellationToken)
    {
        var team = await _db.Teams.SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (team is not null)
        {
            return team;
        }

        team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync(cancellationToken);
        return team;
    }

    private async Task<User> EnsureUserAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        var existing = await _users.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            LockoutEnabled = true,
            CreatedAtUtc = _clock.UtcNow
        };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Development user '{email}' was not created. {errors}");
        }

        return user;
    }

    private async Task EnsureAssignmentAsync(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        var exists = await _db.UserPermissionRoles
            .AnyAsync(link => link.UserId == userId && link.RoleId == roleId, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.UserPermissionRoles.Add(new UserPermissionRole { UserId = userId, RoleId = roleId });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMembershipAsync(Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var exists = await _db.TeamMemberships
            .AnyAsync(membership => membership.TeamId == teamId && membership.UserId == userId, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = userId });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
