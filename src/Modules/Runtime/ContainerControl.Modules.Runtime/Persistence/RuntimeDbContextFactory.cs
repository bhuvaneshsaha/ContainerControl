using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContainerControl.Modules.Runtime.Persistence;

public sealed class RuntimeDbContextFactory : IDesignTimeDbContextFactory<RuntimeDbContext>
{
    public RuntimeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=containercontrol;Username=containercontrol;Password=containercontrol";
        var options = new DbContextOptionsBuilder<RuntimeDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", RuntimeModule.SchemaName);
                npgsql.MigrationsAssembly(typeof(RuntimeDbContext).Assembly.GetName().Name);
            })
            .Options;
        return new RuntimeDbContext(options);
    }
}
