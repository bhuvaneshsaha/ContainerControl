using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Delivery.Persistence;

public sealed class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleBoundary> Boundaries => Set<ModuleBoundary>();

    public DbSet<DeploymentRecord> Deployments => Set<DeploymentRecord>();

    public DbSet<WorkerLease> Leases => Set<WorkerLease>();

    public DbSet<TrafficSlot> TrafficSlots => Set<TrafficSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DeliveryModule.SchemaName);
        modelBuilder.Entity<ModuleBoundary>(entity =>
        {
            entity.ToTable("module_boundary");
            entity.HasKey(boundary => boundary.Id);
            entity.Property(boundary => boundary.ModuleName).HasMaxLength(64).IsRequired();
            entity.HasData(new ModuleBoundary { Id = 1, ModuleName = "Delivery" });
        });
        modelBuilder.Entity<DeploymentRecord>(entity =>
        {
            entity.ToTable("deployments");
            entity.HasKey(deployment => deployment.Id);
            entity.Property(deployment => deployment.Status).HasMaxLength(32).IsRequired();
            entity.Property(deployment => deployment.Mode).HasMaxLength(16).IsRequired();
            entity.Property(deployment => deployment.Error).HasMaxLength(1000);
            entity.Property(deployment => deployment.Hostname).HasMaxLength(253);
            entity.HasIndex(deployment => new { deployment.ApplicationId, deployment.CreatedAtUtc });
        });
        modelBuilder.Entity<WorkerLease>(entity =>
        {
            entity.ToTable("worker_lease");
            entity.HasKey(lease => new { lease.ApplicationId, lease.Environment });
            entity.Property(lease => lease.Environment).HasMaxLength(16).IsRequired();
            entity.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });
        modelBuilder.Entity<TrafficSlot>(entity =>
        {
            entity.ToTable("traffic_slots");
            entity.HasKey(slot => slot.ApplicationId);
            entity.Property(slot => slot.LiveSlot).HasMaxLength(16).IsRequired();
            entity.Property(slot => slot.CandidateSlot).HasMaxLength(16);
            entity.Property(slot => slot.PreviousSlot).HasMaxLength(16);
        });
    }
}
