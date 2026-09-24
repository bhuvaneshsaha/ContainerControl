using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContainerControl.Modules.Platform.Persistence;

public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=containercontrol;Username=containercontrol;Password=containercontrol";
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PlatformModule.SchemaName);
                npgsql.MigrationsAssembly(typeof(PlatformDbContext).Assembly.GetName().Name);
            })
            .Options;
        return new PlatformDbContext(options);
    }
}
