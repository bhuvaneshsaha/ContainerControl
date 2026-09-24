using ContainerControl.Modules.Edge.Domains;
using ContainerControl.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Edge.Persistence;

public sealed class EdgeDbContext : DbContext
{
    public EdgeDbContext(DbContextOptions<EdgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleBoundary> Boundaries => Set<ModuleBoundary>();

    public DbSet<AllowedDomain> Domains => Set<AllowedDomain>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(EdgeModule.SchemaName);
        modelBuilder.Entity<ModuleBoundary>(entity =>
        {
            entity.ToTable("module_boundary");
            entity.HasKey(boundary => boundary.Id);
            entity.Property(boundary => boundary.ModuleName).HasMaxLength(64).IsRequired();
            entity.HasData(new ModuleBoundary { Id = 1, ModuleName = "Edge" });
        });
        modelBuilder.Entity<AllowedDomain>(entity =>
        {
            entity.ToTable("allowed_domains");
            entity.HasKey(domain => domain.Id);
            entity.Property(domain => domain.Name).HasMaxLength(253).IsRequired();
            entity.HasIndex(domain => domain.Name).IsUnique();
        });
    }
}
