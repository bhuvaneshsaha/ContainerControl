using System.Net;
using System.Net.Http.Json;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.Modules.Applications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Host.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DatabaseImageOverrideApiTests
{
    private readonly ApiFixture _fixture;

    public DatabaseImageOverrideApiTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Enabling_database_images_is_forbidden_without_platform_settings_manage()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var developer = factory.CreateClient();
        using var admin = factory.CreateClient();
        var developerUser = await ApiClient.CreateUserAsync(
            factory,
            $"dev-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite);
        var adminUser = await ApiClient.CreateUserAsync(
            factory,
            $"admin-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite,
            PermissionCatalog.PlatformSettingsManage);
        var teamId = await AddMemberAsync(factory, developerUser.Id);
        await AddMemberAsync(factory, adminUser.Id, teamId);
        await developer.SignInAsync(developerUser.Email!, ApiClient.SamplePassword);
        await admin.SignInAsync(adminUser.Email!, ApiClient.SamplePassword);
        var hostId = Guid.NewGuid();
        var name = "db-" + Guid.NewGuid().ToString("N")[..8];

        var deniedCreate = await developer.SendAsync(HttpMethod.Post, "/apps", AppBody(teamId, hostId, name, allowDatabaseImages: true));
        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);
        Assert.Contains("Manage platform settings", await deniedCreate.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var created = await developer.SendAsync(HttpMethod.Post, "/apps", AppBody(teamId, hostId, name, allowDatabaseImages: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appId = (await created.Content.ReadFromJsonAsync<IdBody>())!.Id;
        var stored = await GetAppAsync(developer, appId);
        Assert.False(stored.AllowDatabaseImages);

        var deniedUpdate = await developer.SendAsync(HttpMethod.Put, $"/apps/{appId}", AppBody(teamId, hostId, name, allowDatabaseImages: true));
        Assert.Equal(HttpStatusCode.Forbidden, deniedUpdate.StatusCode);
        Assert.False((await GetAppAsync(developer, appId)).AllowDatabaseImages);

        var allowed = await admin.SendAsync(HttpMethod.Put, $"/apps/{appId}", AppBody(teamId, hostId, name, allowDatabaseImages: true));
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.True((await GetAppAsync(admin, appId)).AllowDatabaseImages);

        var cleared = await developer.SendAsync(HttpMethod.Put, $"/apps/{appId}", AppBody(teamId, hostId, name, allowDatabaseImages: false));
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.False((await GetAppAsync(developer, appId)).AllowDatabaseImages);

        var omitted = await admin.SendAsync(HttpMethod.Put, $"/apps/{appId}", AppBody(teamId, hostId, name, allowDatabaseImages: true));
        omitted.EnsureSuccessStatusCode();
        var kept = await developer.SendAsync(HttpMethod.Put, $"/apps/{appId}", AppBody(teamId, hostId, name, allowDatabaseImages: null));
        Assert.Equal(HttpStatusCode.NoContent, kept.StatusCode);
        Assert.True((await GetAppAsync(developer, appId)).AllowDatabaseImages);

        await using var scope = factory.Services.CreateAsyncScope();
        var apps = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        var row = await apps.Apps.SingleAsync(app => app.Id == appId);
        Assert.True(row.AllowDatabaseImages);

        var audit = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var actions = await audit.AuditEntries
            .Where(entry => entry.SubjectId == appId.ToString())
            .Select(entry => entry.Action)
            .ToListAsync();
        Assert.Contains("apps.created", actions);
        Assert.Contains("apps.database-images.allowed", actions);
        Assert.Contains("apps.database-images.blocked", actions);
    }

    private static Dictionary<string, object?> AppBody(Guid teamId, Guid hostId, string name, bool? allowDatabaseImages)
    {
        var body = new Dictionary<string, object?>
        {
            ["teamId"] = teamId,
            ["hostId"] = hostId,
            ["name"] = name,
            ["environment"] = "dev",
            ["composeYaml"] = "services:\n  web:\n    image: nginx:1.27\n",
            ["exposed"] = false,
            ["requiresApproval"] = false
        };
        if (allowDatabaseImages is not null)
        {
            body["allowDatabaseImages"] = allowDatabaseImages.Value;
        }

        return body;
    }

    private static async Task<AppFlag> GetAppAsync(HttpClient client, Guid appId)
    {
        var response = await client.GetAsync($"/apps/{appId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AppFlag>())!;
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

    private sealed record AppFlag(bool AllowDatabaseImages);
}
