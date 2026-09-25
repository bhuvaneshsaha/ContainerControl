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
public sealed class TemplateMarketplaceTests
{
    private const string SecretValue = "super-secret-value";

    private const string SafeCompose = """
        services:
          web:
            image: nginx:stable
            environment:
              GREETING: hello
              DATABASE_PASSWORD: ${DATABASE_PASSWORD}
        """;

    private readonly ApiFixture _fixture;

    public TemplateMarketplaceTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Admin_publishes_and_developer_creates_an_application_from_the_template()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var admin = factory.CreateClient();
        using var developer = factory.CreateClient();
        using var reader = factory.CreateClient();
        var adminUser = await ApiClient.CreateUserAsync(factory, $"tpl-admin-{Guid.NewGuid():N}@localhost", PermissionCatalog.AppsTemplatesManage, PermissionCatalog.AppsRead);
        var developerUser = await ApiClient.CreateUserAsync(
            factory,
            $"tpl-dev-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite);
        var readerUser = await ApiClient.CreateUserAsync(factory, $"tpl-read-{Guid.NewGuid():N}@localhost", PermissionCatalog.AppsRead);
        var teamId = await AddMemberAsync(factory, developerUser.Id);
        await admin.SignInAsync(adminUser.Email!, ApiClient.SamplePassword);
        await developer.SignInAsync(developerUser.Email!, ApiClient.SamplePassword);
        await reader.SignInAsync(readerUser.Email!, ApiClient.SamplePassword);

        var deniedPublish = await developer.SendAsync(HttpMethod.Post, "/templates", new
        {
            name = "nginx",
            description = "A web server.",
            composeYaml = SafeCompose
        });
        Assert.Equal(HttpStatusCode.Forbidden, deniedPublish.StatusCode);

        var name = "nginx-" + Guid.NewGuid().ToString("N")[..8];
        var published = await admin.SendAsync(HttpMethod.Post, "/templates", new
        {
            name,
            description = "A web server.",
            composeYaml = SafeCompose
        });
        Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        var templateId = (await published.Content.ReadFromJsonAsync<IdBody>())!.Id;

        var listed = await developer.GetAsync("/templates");
        listed.EnsureSuccessStatusCode();
        var list = await listed.Content.ReadFromJsonAsync<TemplateListBody>();
        var template = Assert.Single(list!.Templates, item => item.Id == templateId);
        Assert.Equal(SafeCompose, template.ComposeYaml);
        Assert.DoesNotContain(SecretValue, template.ComposeYaml, StringComparison.Ordinal);

        var duplicate = await admin.SendAsync(HttpMethod.Post, "/templates", new
        {
            name = name.ToUpperInvariant(),
            description = "Another copy.",
            composeYaml = SafeCompose
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var rejected = await admin.SendAsync(HttpMethod.Post, "/templates", new
        {
            name = "secret-" + Guid.NewGuid().ToString("N")[..8],
            description = "Should not be stored.",
            composeYaml = "services:\n  web:\n    image: nginx:stable\n    environment:\n      API_TOKEN: " + SecretValue + "\n"
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("secret value", rejectedBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretValue, rejectedBody, StringComparison.Ordinal);

        var privileged = await admin.SendAsync(HttpMethod.Post, "/templates", new
        {
            name = "priv-" + Guid.NewGuid().ToString("N")[..8],
            description = "Privileged is not allowed.",
            composeYaml = "services:\n  web:\n    image: nginx:stable\n    privileged: true\n"
        });
        Assert.Equal(HttpStatusCode.BadRequest, privileged.StatusCode);
        Assert.Contains("privileged", await privileged.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var deniedCreate = await reader.SendAsync(HttpMethod.Post, "/apps/from-template", new
        {
            templateId,
            teamId,
            hostId = Guid.NewGuid(),
            name = "from-template",
            environment = "dev",
            exposed = false,
            requiresApproval = false,
            composeYaml = "services:\n  web:\n    image: busybox:1.36.1\n"
        });
        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);

        var hostId = Guid.NewGuid();
        var appName = "from-" + Guid.NewGuid().ToString("N")[..8];
        var created = await developer.SendAsync(HttpMethod.Post, "/apps/from-template", new
        {
            templateId,
            teamId,
            hostId,
            name = appName,
            environment = "dev",
            hostname = "web.apps.localhost",
            exposed = true,
            requiresApproval = false,
            composeYaml = "services:\n  web:\n    image: busybox:1.36.1\n      API_TOKEN: " + SecretValue + "\n"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appId = (await created.Content.ReadFromJsonAsync<IdBody>())!.Id;
        var app = await developer.GetAsync($"/apps/{appId}");
        app.EnsureSuccessStatusCode();
        var appBody = await app.Content.ReadAsStringAsync();
        Assert.Contains("nginx:stable", appBody, StringComparison.Ordinal);
        Assert.Contains("${DATABASE_PASSWORD}", appBody, StringComparison.Ordinal);
        Assert.DoesNotContain("busybox", appBody, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, appBody, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var apps = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
            var access = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            var stored = await apps.Templates.SingleAsync(item => item.Id == templateId);
            Assert.Equal(SafeCompose, stored.ComposeYaml);
            Assert.DoesNotContain(SecretValue, stored.ComposeYaml, StringComparison.Ordinal);
            var leaked = await apps.Templates.AnyAsync(item => item.ComposeYaml.Contains(SecretValue) || item.Description.Contains(SecretValue) || item.Name.Contains(SecretValue));
            Assert.False(leaked);
            var audits = await access.AuditEntries.Where(entry => entry.SubjectId == templateId.ToString()).ToListAsync();
            Assert.Contains(audits, entry => entry.Action == "apps.templates.published");
            Assert.All(audits, entry => Assert.DoesNotContain(SecretValue, entry.Action + entry.SubjectType + entry.SubjectId, StringComparison.Ordinal));
        }

        var removed = await admin.SendAsync(HttpMethod.Delete, $"/templates/{templateId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var after = await developer.GetAsync("/templates");
        var remaining = await after.Content.ReadFromJsonAsync<TemplateListBody>();
        Assert.DoesNotContain(remaining!.Templates, item => item.Id == templateId);
        var stillThere = await developer.GetAsync($"/apps/{appId}");
        stillThere.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Listing_templates_requires_an_application_or_template_permission()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        var user = await ApiClient.CreateUserAsync(factory, $"tpl-none-{Guid.NewGuid():N}@localhost", PermissionCatalog.RegistriesRead);
        await client.SignInAsync(user.Email!, ApiClient.SamplePassword);
        var response = await client.GetAsync("/templates");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private sealed record TemplateBody(Guid Id, string Name, string Description, string ComposeYaml);

    private sealed record TemplateListBody(IReadOnlyList<TemplateBody> Templates);
}
