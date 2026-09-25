using ContainerControl.Modules.Platform.Alerts;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Platform.Quotas;
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

    public DbSet<TeamQuota> Quotas => Set<TeamQuota>();

    public DbSet<HostCapacityReading> Capacity => Set<HostCapacityReading>();

    public DbSet<AlertSetting> AlertSettings => Set<AlertSetting>();

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
            entity.Property(host => host.ClientCertRef).HasMaxLength(500);
            entity.Property(host => host.ClientKeyRef).HasMaxLength(500);
            entity.Property(host => host.CaRef).HasMaxLength(500);
            entity.Property(host => host.EngineVersion).HasMaxLength(64);
            entity.HasIndex(host => host.Name).IsUnique();
            entity.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });
        modelBuilder.Entity<TeamQuota>(entity =>
        {
            entity.ToTable("team_quotas");
            entity.HasKey(quota => quota.TeamId);
        });
        modelBuilder.Entity<HostCapacityReading>(entity =>
        {
            entity.ToTable("host_capacity");
            entity.HasKey(reading => reading.HostId);
        });
        modelBuilder.Entity<AlertSetting>(entity =>
        {
            entity.ToTable("alert_settings");
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Id).ValueGeneratedNever();
            entity.Property(setting => setting.WebhookUrl).HasMaxLength(2000);
            entity.Property(setting => setting.Recipients).HasMaxLength(2000);
        });
    }
}
