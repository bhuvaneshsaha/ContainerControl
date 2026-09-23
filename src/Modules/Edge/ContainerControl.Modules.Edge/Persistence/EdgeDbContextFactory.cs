using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContainerControl.Modules.Edge.Persistence;

public sealed class EdgeDbContextFactory : IDesignTimeDbContextFactory<EdgeDbContext>
{
    public EdgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=containercontrol;Username=containercontrol;Password=containercontrol";
        var options = new DbContextOptionsBuilder<EdgeDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", EdgeModule.SchemaName);
                npgsql.MigrationsAssembly(typeof(EdgeDbContext).Assembly.GetName().Name);
            })
            .Options;
        return new EdgeDbContext(options);
    }
}
