using System.Net;
using System.Net.Http.Json;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.Modules.Applications.Persistence;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Platform.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Host.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SecretTargetDeployTests
{
    private readonly ApiFixture _fixture;

    public SecretTargetDeployTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deploy_rejects_a_service_that_references_an_unassigned_secret()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        var user = await ApiClient.CreateUserAsync(
            factory,
            $"scope-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite,
            PermissionCatalog.DeployExecute);
        var teamId = await AddMemberAsync(factory, user.Id);
        await client.SignInAsync(user.Email!, ApiClient.SamplePassword);
        var hostId = await AddHostAsync(factory);

        var appId = await CreateAppAsync(client, teamId, hostId, "scoped-app");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
            var secret = new SecretReference
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                Environment = "dev",
                Name = "DB_PASSWORD",
                Path = $"/teams/{teamId:N}/DB_PASSWORD",
                InjectionMode = "env",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            secret.Targets.Add(new SecretServiceTarget { SecretId = secret.Id, ServiceName = "api" });
            db.Secrets.Add(secret);
            await db.SaveChangesAsync();
        }

        var rejected = await client.SendAsync(HttpMethod.Post, $"/apps/{appId}/deploy");
        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains(
            "Service 'worker' references secret 'DB_PASSWORD', which is not assigned to that service.",
            body,
            StringComparison.Ordinal);

        var app = await client.GetFromJsonAsync<AppStatus>($"/apps/{appId}");
        Assert.Equal("rejected", app!.Status);
    }

    [Fact]
    public async Task Deploy_does_not_treat_a_secret_with_no_targets_as_assigned_to_every_service()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        var user = await ApiClient.CreateUserAsync(
            factory,
            $"legacy-deploy-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite,
            PermissionCatalog.DeployExecute);
        var teamId = await AddMemberAsync(factory, user.Id);
        await client.SignInAsync(user.Email!, ApiClient.SamplePassword);
        var hostId = await AddHostAsync(factory);
        var appId = await CreateAppAsync(client, teamId, hostId, "legacy-app");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
            db.Secrets.Add(new SecretReference
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                Environment = "dev",
                Name = "DB_PASSWORD",
                Path = $"/teams/{teamId:N}/DB_PASSWORD",
                InjectionMode = "env",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var rejected = await client.SendAsync(HttpMethod.Post, $"/apps/{appId}/deploy");
        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("not assigned to that service", body, StringComparison.Ordinal);
    }

    private static async Task<Guid> CreateAppAsync(HttpClient client, Guid teamId, Guid hostId, string name)
    {
        var created = await client.SendAsync(HttpMethod.Post, "/apps", new
        {
            teamId,
            hostId,
            name,
            environment = "dev",
            composeYaml = """
                services:
                  api:
                    image: busybox:1.36.1
                  worker:
                    image: busybox:1.36.1
                    environment:
                      APP_PASSWORD: ${DB_PASSWORD}
                    command: ["myapp", "${DB_PASSWORD}"]
                """
        });
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<IdBody>();
        return body!.Id;
    }

    private static async Task<Guid> AddHostAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var id = Guid.NewGuid();
        db.Hosts.Add(new DockerHost
        {
            Id = id,
            Name = "host-" + id.ToString("N")[..8],
            Endpoint = "unix:///var/run/docker.sock",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> AddMemberAsync(ApiFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var id = Guid.NewGuid();
        db.Teams.Add(new Team { Id = id, Name = "team-" + id.ToString("N")[..8], CreatedAtUtc = DateTimeOffset.UtcNow });
        db.TeamMemberships.Add(new TeamMembership { TeamId = id, UserId = userId });
        await db.SaveChangesAsync();
        return id;
    }

    private sealed record IdBody(Guid Id);

    private sealed record AppStatus(string Status);
}
