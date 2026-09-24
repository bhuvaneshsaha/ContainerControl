using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContainerControl.Modules.Registries.Persistence;

public sealed class RegistriesDbContextFactory : IDesignTimeDbContextFactory<RegistriesDbContext>
{
    public RegistriesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=containercontrol;Username=containercontrol;Password=containercontrol";
        var options = new DbContextOptionsBuilder<RegistriesDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RegistriesModule.SchemaName);
                npgsql.MigrationsAssembly(typeof(RegistriesDbContext).Assembly.GetName().Name);
            })
            .Options;
        return new RegistriesDbContext(options);
    }
}
