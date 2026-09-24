using ContainerControl.Modules.Access.Domain;
using ContainerControl.Modules.Access.Domain.Auditing;
using ContainerControl.Modules.Access.Domain.BreakGlass;
using ContainerControl.Modules.Access.Domain.Roles;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Domain.Tokens;
using ContainerControl.Modules.Access.Domain.Users;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Access.Infrastructure.Persistence;

public sealed class AccessDbContext : IdentityUserContext<User, Guid>
{
    public AccessDbContext(DbContextOptions<AccessDbContext> options)
        : base(options)
    {
    }

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();

    public DbSet<Domain.Permissions.Permission> Permissions => Set<Domain.Permissions.Permission>();

    public DbSet<PermissionRole> PermissionRoles => Set<PermissionRole>();

    public DbSet<PermissionRolePermission> PermissionRolePermissions => Set<PermissionRolePermission>();

    public DbSet<UserPermissionRole> UserPermissionRoles => Set<UserPermissionRole>();

    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    public DbSet<BreakGlassGrant> BreakGlassGrants => Set<BreakGlassGrant>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AccessSchema.Name);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
    }
}
