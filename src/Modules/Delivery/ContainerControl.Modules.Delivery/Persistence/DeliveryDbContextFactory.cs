using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContainerControl.Modules.Delivery.Persistence;

public sealed class DeliveryDbContextFactory : IDesignTimeDbContextFactory<DeliveryDbContext>
{
    public DeliveryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=containercontrol;Username=containercontrol;Password=containercontrol";
        var options = new DbContextOptionsBuilder<DeliveryDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DeliveryModule.SchemaName);
                npgsql.MigrationsAssembly(typeof(DeliveryDbContext).Assembly.GetName().Name);
            })
            .Options;
        return new DeliveryDbContext(options);
    }
}
