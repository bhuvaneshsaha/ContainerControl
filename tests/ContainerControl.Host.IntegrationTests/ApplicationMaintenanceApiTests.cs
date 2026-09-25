using System.Net;
using System.Net.Http.Json;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.Modules.Delivery.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Host.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ApplicationMaintenanceApiTests
{
    private readonly ApiFixture _fixture;

    public ApplicationMaintenanceApiTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Update_keeps_team_and_environment_and_delete_removes_history()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var developer = factory.CreateClient();
        using var reader = factory.CreateClient();
        using var outsider = factory.CreateClient();
        var developerUser = await ApiClient.CreateUserAsync(
            factory,
            $"edit-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite);
        var readerUser = await ApiClient.CreateUserAsync(
            factory,
            $"read-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead);
        var outsiderUser = await ApiClient.CreateUserAsync(
            factory,
            $"out-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite);
        var teamId = await AddMemberAsync(factory, developerUser.Id);
        await AddMemberAsync(factory, readerUser.Id, teamId);
        await developer.SignInAsync(developerUser.Email!, ApiClient.SamplePassword);
        await reader.SignInAsync(readerUser.Email!, ApiClient.SamplePassword);
        await outsider.SignInAsync(outsiderUser.Email!, ApiClient.SamplePassword);

        var hostId = Guid.NewGuid();
        var nextHostId = Guid.NewGuid();
        var name = "edit-" + Guid.NewGuid().ToString("N")[..8];
        var created = await developer.SendAsync(HttpMethod.Post, "/apps", new
        {
            teamId,
            hostId,
            name,
            environment = "dev",
            image = "nginx:1.27",
            command = new[] { "nginx", "-g", "daemon off;" },
            internalPort = 80,
            hostname = "web.apps.localhost",
            exposed = true,
            requiresApproval = false
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appId = (await created.Content.ReadFromJsonAsync<IdBody>())!.Id;

        var loaded = await GetAppAsync(developer, appId);
        Assert.Equal(name, loaded.Name);
        Assert.Equal(teamId, loaded.TeamId);
        Assert.Equal("dev", loaded.Environment);
        Assert.Equal(new[] { "nginx", "-g", "daemon off;" }, loaded.Command);

        var otherTeam = Guid.NewGuid();
        var renamed = name + "-renamed";
        var updated = await developer.SendAsync(HttpMethod.Put, $"/apps/{appId}", new
        {
            teamId = otherTeam,
            hostId = nextHostId,
            name = renamed,
            environment = "prod",
            image = (string?)null,
            command = new[] { "nginx", "-g", "daemon off;" },
            composeYaml = "services:\n  web:\n    image: nginx:1.27\n",
            internalPort = 8080,
            hostname = "web.apps.localhost",
            exposed = false,
            requiresApproval = true
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var after = await GetAppAsync(developer, appId);
        Assert.Equal(renamed, after.Name);
        Assert.Equal(nextHostId, after.HostId);
        Assert.Equal(teamId, after.TeamId);
        Assert.Equal("dev", after.Environment);
        Assert.Equal("services:\n  web:\n    image: nginx:1.27\n", after.ComposeYaml);
        Assert.Equal(8080, after.InternalPort);
        Assert.False(after.Exposed);
        Assert.True(after.RequiresApproval);
        Assert.Equal(new[] { "nginx", "-g", "daemon off;" }, after.Command);

        var blank = await developer.SendAsync(HttpMethod.Put, $"/apps/{appId}", new
        {
            name = "   ",
            composeYaml = after.ComposeYaml,
            exposed = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Contains("application name", await blank.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(renamed, (await GetAppAsync(developer, appId)).Name);

        var sibling = "sib-" + Guid.NewGuid().ToString("N")[..8];
        var second = await developer.SendAsync(HttpMethod.Post, "/apps", new
        {
            teamId,
            hostId,
            name = sibling,
            environment = "dev",
            composeYaml = "services:\n  web:\n    image: nginx:1.27\n",
            exposed = false
        });
        second.EnsureSuccessStatusCode();
        var duplicate = await developer.SendAsync(HttpMethod.Put, $"/apps/{appId}", new
        {
            name = sibling,
            composeYaml = after.ComposeYaml,
            exposed = false
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already exists", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(renamed, (await GetAppAsync(developer, appId)).Name);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var delivery = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
            delivery.Deployments.Add(new DeploymentRecord
            {
                Id = Guid.NewGuid(),
                ApplicationId = appId,
                Status = "succeeded",
                ComposeYaml = after.ComposeYaml,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            delivery.Leases.Add(new WorkerLease
            {
                ApplicationId = appId,
                Environment = "dev"
            });
            await delivery.SaveChangesAsync();
        }

        var denied = await reader.DeleteAsync($"/apps/{appId}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var hidden = await outsider.DeleteAsync($"/apps/{appId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await developer.DeleteAsync($"/apps/{Guid.NewGuid()}")).StatusCode);

        var removed = await developer.DeleteAsync($"/apps/{appId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await developer.GetAsync($"/apps/{appId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await developer.DeleteAsync($"/apps/{appId}")).StatusCode);

        await using var check = factory.Services.CreateAsyncScope();
        var history = check.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        Assert.Empty(await history.Deployments.Where(item => item.ApplicationId == appId).ToListAsync());
        Assert.Empty(await history.Leases.Where(item => item.ApplicationId == appId).ToListAsync());
        var audit = check.ServiceProvider.GetRequiredService<AccessDbContext>();
        var actions = await audit.AuditEntries
            .Where(entry => entry.SubjectId == appId.ToString())
            .Select(entry => entry.Action)
            .ToListAsync();
        Assert.Contains("apps.updated", actions);
        Assert.Contains("apps.deleted", actions);
    }

    [Fact]
    public async Task Delete_is_refused_while_a_deploy_lease_is_held()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var developer = factory.CreateClient();
        var developerUser = await ApiClient.CreateUserAsync(
            factory,
            $"lease-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite);
        var teamId = await AddMemberAsync(factory, developerUser.Id);
        await developer.SignInAsync(developerUser.Email!, ApiClient.SamplePassword);
        var name = "lease-" + Guid.NewGuid().ToString("N")[..8];
        var created = await developer.SendAsync(HttpMethod.Post, "/apps", new
        {
            teamId,
            hostId = Guid.NewGuid(),
            name,
            environment = "dev",
            composeYaml = "services:\n  web:\n    image: nginx:1.27\n",
            exposed = false
        });
        created.EnsureSuccessStatusCode();
        var appId = (await created.Content.ReadFromJsonAsync<IdBody>())!.Id;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var delivery = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
            delivery.Leases.Add(new WorkerLease
            {
                ApplicationId = appId,
                Environment = "dev",
                OwnerId = Guid.NewGuid(),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
            });
            await delivery.SaveChangesAsync();
        }

        var refused = await developer.DeleteAsync($"/apps/{appId}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains(DeployLease.ContendedMessage, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        (await developer.GetAsync($"/apps/{appId}")).EnsureSuccessStatusCode();
    }

    private static async Task<AppView> GetAppAsync(HttpClient client, Guid appId)
    {
        var response = await client.GetAsync($"/apps/{appId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AppView>())!;
    }

    private static async Task<Guid> AddMemberAsync(ApiFactory factory, Guid userId, Guid? teamId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var id = teamId ?? Guid.NewGuid();
        if (teamId is null)
        {
            db.Teams.Add(new Team { Id = id, Name = "team-" + id.ToString("N")[..8], CreatedAtUtc = DateTimeOffset.UtcNow });
        }

        db.TeamMemberships.Add(new TeamMembership { TeamId = id, UserId = userId });
        await db.SaveChangesAsync();
        return id;
    }

    private sealed record IdBody(Guid Id);

    private sealed record AppView(
        Guid TeamId,
        Guid HostId,
        string Name,
        string Environment,
        string? ComposeYaml,
        int? InternalPort,
        bool Exposed,
        bool RequiresApproval,
        IReadOnlyList<string>? Command);
}
