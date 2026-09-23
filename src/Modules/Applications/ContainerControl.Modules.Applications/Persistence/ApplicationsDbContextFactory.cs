using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContainerControl.Modules.Applications.Persistence;

public sealed class ApplicationsDbContextFactory : IDesignTimeDbContextFactory<ApplicationsDbContext>
{
    public ApplicationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=containercontrol;Username=containercontrol;Password=containercontrol";
        var options = new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationsModule.SchemaName);
                npgsql.MigrationsAssembly(typeof(ApplicationsDbContext).Assembly.GetName().Name);
            })
            .Options;
        return new ApplicationsDbContext(options);
    }
}
