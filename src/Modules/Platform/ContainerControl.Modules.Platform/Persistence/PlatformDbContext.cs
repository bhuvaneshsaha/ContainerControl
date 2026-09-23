using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Platform.Persistence;

public sealed class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleBoundary> Boundaries => Set<ModuleBoundary>();

    public DbSet<DockerHost> Hosts => Set<DockerHost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PlatformModule.SchemaName);
        modelBuilder.Entity<ModuleBoundary>(entity =>
        {
            entity.ToTable("module_boundary");
            entity.HasKey(boundary => boundary.Id);
            entity.Property(boundary => boundary.ModuleName).HasMaxLength(64).IsRequired();
            entity.HasData(new ModuleBoundary { Id = 1, ModuleName = "Platform" });
        });
        modelBuilder.Entity<DockerHost>(entity =>
        {
            entity.ToTable("docker_hosts");
            entity.HasKey(host => host.Id);
            entity.Property(host => host.Name).HasMaxLength(200).IsRequired();
            entity.Property(host => host.Endpoint).HasMaxLength(500).IsRequired();
            entity.Property(host => host.EngineVersion).HasMaxLength(64);
            entity.HasIndex(host => host.Name).IsUnique();
            entity.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });
    }
}
