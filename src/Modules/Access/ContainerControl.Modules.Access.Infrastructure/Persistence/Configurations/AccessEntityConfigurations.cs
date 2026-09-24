using ContainerControl.Modules.Access.Domain.Auditing;
using ContainerControl.Modules.Access.Domain.BreakGlass;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Roles;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Domain.Tokens;
using ContainerControl.Modules.Access.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContainerControl.Modules.Access.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();
    }
}

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.HasKey(team => team.Id);
        builder.Property(team => team.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(team => team.Name).IsUnique();
        builder.HasMany(team => team.Memberships)
            .WithOne(membership => membership.Team)
            .HasForeignKey(membership => membership.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("team_memberships");
        builder.HasKey(membership => new { membership.TeamId, membership.UserId });
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(permission => permission.Code);
        builder.Property(permission => permission.Code).HasMaxLength(128);
        builder.Property(permission => permission.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(permission => permission.Module).HasMaxLength(64).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(400).IsRequired();
        builder.HasData(PermissionCatalog.All.Select(permission => new Permission
        {
            Code = permission.Code,
            DisplayName = permission.DisplayName,
            Module = permission.Module,
            Description = permission.Description
        }).ToArray());
    }
}

public sealed class PermissionRoleConfiguration : IEntityTypeConfiguration<PermissionRole>
{
    public void Configure(EntityTypeBuilder<PermissionRole> builder)
    {
        builder.ToTable("permission_roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(200).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(400);
        builder.HasIndex(role => role.Name).IsUnique();
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        builder.HasMany(role => role.Permissions)
            .WithOne(link => link.Role)
            .HasForeignKey(link => link.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(role => role.Users)
            .WithOne(link => link.Role)
            .HasForeignKey(link => link.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PermissionRolePermissionConfiguration : IEntityTypeConfiguration<PermissionRolePermission>
{
    public void Configure(EntityTypeBuilder<PermissionRolePermission> builder)
    {
        builder.ToTable("permission_role_permissions");
        builder.HasKey(link => new { link.RoleId, link.PermissionCode });
        builder.Property(link => link.PermissionCode).HasMaxLength(128);
        builder.HasOne(link => link.Permission)
            .WithMany()
            .HasForeignKey(link => link.PermissionCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserPermissionRoleConfiguration : IEntityTypeConfiguration<UserPermissionRole>
{
    public void Configure(EntityTypeBuilder<UserPermissionRole> builder)
    {
        builder.ToTable("user_permission_roles");
        builder.HasKey(link => new { link.UserId, link.RoleId });
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(link => link.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("api_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Name).HasMaxLength(200).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(256);
        builder.HasIndex(token => token.UserId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BreakGlassGrantConfiguration : IEntityTypeConfiguration<BreakGlassGrant>
{
    public void Configure(EntityTypeBuilder<BreakGlassGrant> builder)
    {
        builder.ToTable("break_glass_grants");
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.PermissionCode).HasMaxLength(128).IsRequired();
        builder.HasIndex(grant => grant.UserId);
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Action).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.SubjectType).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.SubjectId).HasMaxLength(64);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(128).IsRequired();
        builder.HasIndex(entry => entry.OccurredAtUtc);
    }
}
