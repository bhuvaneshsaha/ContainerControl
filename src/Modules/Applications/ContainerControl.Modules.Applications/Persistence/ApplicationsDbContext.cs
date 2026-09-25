using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Templates;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Applications.Persistence;

public sealed class ApplicationsDbContext : DbContext
{
    public ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleBoundary> Boundaries => Set<ModuleBoundary>();

    public DbSet<ContainerApp> Apps => Set<ContainerApp>();

    public DbSet<SecretReference> Secrets => Set<SecretReference>();

    public DbSet<SecretServiceTarget> SecretServiceTargets => Set<SecretServiceTarget>();

    public DbSet<AppTemplate> Templates => Set<AppTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ApplicationsModule.SchemaName);
        modelBuilder.Entity<ModuleBoundary>(entity =>
        {
            entity.ToTable("module_boundary");
            entity.HasKey(boundary => boundary.Id);
            entity.Property(boundary => boundary.ModuleName).HasMaxLength(64).IsRequired();
            entity.HasData(new ModuleBoundary { Id = 1, ModuleName = "Applications" });
        });
        modelBuilder.Entity<ContainerApp>(entity =>
        {
            entity.ToTable("apps");
            entity.HasKey(app => app.Id);
            entity.Property(app => app.Name).HasMaxLength(200).IsRequired();
            entity.Property(app => app.Environment).HasMaxLength(16).IsRequired();
            entity.Property(app => app.Image).HasMaxLength(500);
            entity.Property(app => app.Hostname).HasMaxLength(253);
            entity.Property(app => app.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(app => new { app.TeamId, app.Name, app.Environment }).IsUnique();
            entity.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });
        modelBuilder.Entity<SecretReference>(entity =>
        {
            entity.ToTable("secret_references");
            entity.HasKey(secret => secret.Id);
            entity.Property(secret => secret.Environment).HasMaxLength(16).IsRequired();
            entity.Property(secret => secret.Name).HasMaxLength(200).IsRequired();
            entity.Property(secret => secret.Path).HasMaxLength(500).IsRequired();
            entity.Property(secret => secret.InjectionMode).HasMaxLength(16).IsRequired();
            entity.HasIndex(secret => new { secret.TeamId, secret.Environment, secret.Name }).IsUnique();
            entity.HasMany(secret => secret.Targets)
                .WithOne()
                .HasForeignKey(target => target.SecretId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SecretServiceTarget>(entity =>
        {
            entity.ToTable("secret_service_targets");
            entity.HasKey(target => new { target.SecretId, target.ServiceName });
            entity.Property(target => target.ServiceName).HasMaxLength(63).IsRequired();
        });
        modelBuilder.Entity<AppTemplate>(entity =>
        {
            entity.ToTable("app_templates");
            entity.HasKey(template => template.Id);
            entity.Property(template => template.Name).HasMaxLength(TemplateAdmin.MaxNameLength).IsRequired();
            entity.Property(template => template.NameKey).HasMaxLength(TemplateAdmin.MaxNameLength).IsRequired();
            entity.Property(template => template.Description).HasMaxLength(TemplateAdmin.MaxDescriptionLength).IsRequired();
            entity.Property(template => template.ComposeYaml).IsRequired();
            entity.HasIndex(template => template.NameKey).IsUnique();
        });
    }
}
