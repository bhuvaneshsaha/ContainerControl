using ContainerControl.Modules.Registries.Connections;
using ContainerControl.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Registries.Persistence;

public sealed class RegistriesDbContext : DbContext
{
    public RegistriesDbContext(DbContextOptions<RegistriesDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleBoundary> Boundaries => Set<ModuleBoundary>();

    public DbSet<RegistryConnection> Connections => Set<RegistryConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(RegistriesModule.SchemaName);
        modelBuilder.Entity<ModuleBoundary>(entity =>
        {
            entity.ToTable("module_boundary");
            entity.HasKey(boundary => boundary.Id);
            entity.Property(boundary => boundary.ModuleName).HasMaxLength(64).IsRequired();
            entity.HasData(new ModuleBoundary { Id = 1, ModuleName = "Registries" });
        });
        modelBuilder.Entity<RegistryConnection>(entity =>
        {
            entity.ToTable("registry_connections");
            entity.HasKey(connection => connection.Id);
            entity.Property(connection => connection.Name).HasMaxLength(128).IsRequired();
            entity.Property(connection => connection.Kind).HasMaxLength(32).IsRequired();
            entity.Property(connection => connection.Server).HasMaxLength(256).IsRequired();
            entity.Property(connection => connection.Environment).HasMaxLength(16).IsRequired();
            entity.Property(connection => connection.UsernamePath).HasMaxLength(128).IsRequired();
            entity.Property(connection => connection.PasswordPath).HasMaxLength(128).IsRequired();
            entity.Property(connection => connection.AccessKeyPath).HasMaxLength(128);
            entity.Property(connection => connection.SecretKeyPath).HasMaxLength(128);
        });
    }
}
