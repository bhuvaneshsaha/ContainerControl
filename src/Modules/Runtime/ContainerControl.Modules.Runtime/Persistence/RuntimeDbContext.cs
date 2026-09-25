using ContainerControl.Modules.Runtime.Inspection;
using ContainerControl.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Runtime.Persistence;

public sealed class RuntimeDbContext : DbContext
{
    public RuntimeDbContext(DbContextOptions<RuntimeDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleBoundary> Boundaries => Set<ModuleBoundary>();

    public DbSet<StoredLogLine> LogLines => Set<StoredLogLine>();

    public DbSet<LogCursor> LogCursors => Set<LogCursor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(RuntimeModule.SchemaName);
        modelBuilder.Entity<ModuleBoundary>(entity =>
        {
            entity.ToTable("module_boundary");
            entity.HasKey(boundary => boundary.Id);
            entity.Property(boundary => boundary.ModuleName).HasMaxLength(64).IsRequired();
            entity.HasData(new ModuleBoundary { Id = 1, ModuleName = "Runtime" });
        });
        modelBuilder.Entity<StoredLogLine>(entity =>
        {
            entity.ToTable("log_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.ContainerId).HasMaxLength(64).IsRequired();
            entity.Property(line => line.Service).HasMaxLength(128).IsRequired();
            entity.Property(line => line.Text).HasMaxLength(StoredLogText.MaxLength).IsRequired();
            entity.Property(line => line.TextHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(line => new { line.ApplicationId, line.RecordedAtUtc });
            entity.HasIndex(line => new { line.ContainerId, line.RecordedAtUtc, line.TextHash }).IsUnique();
        });
        modelBuilder.Entity<LogCursor>(entity =>
        {
            entity.ToTable("log_cursors");
            entity.HasKey(cursor => cursor.ContainerId);
            entity.Property(cursor => cursor.ContainerId).HasMaxLength(64);
        });
    }
}
