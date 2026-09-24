using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace ContainerControl.Host.IntegrationTests;

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("containercontrol")
        .WithUsername("containercontrol")
        .WithPassword("containercontrol")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public ApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();
        Factory = new ApiFactory(ConnectionString);
        _ = Factory.CreateClient();
    }

    public async Task<string> CreateDatabaseAsync(string databaseName)
    {
        await _postgres.ExecScriptAsync($"CREATE DATABASE \"{databaseName}\";");
        var connectionString = _postgres.GetConnectionString();
        return connectionString.Replace(
            "Database=containercontrol",
            $"Database={databaseName}",
            StringComparison.Ordinal);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _environment;
    private readonly Dictionary<string, string?> _settings;

    public ApiFactory(
        string connectionString,
        string environment = "Testing",
        Dictionary<string, string?>? settings = null)
    {
        _connectionString = connectionString;
        _environment = environment;
        _settings = settings ?? [];
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Database", _connectionString);
        foreach (var (key, value) in _settings)
        {
            if (value is not null)
            {
                builder.UseSetting(key, value);
            }
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
