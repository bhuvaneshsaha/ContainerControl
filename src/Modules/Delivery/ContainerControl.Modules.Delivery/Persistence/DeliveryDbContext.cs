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
    }
}
