using System.Net;
using System.Net.Http.Json;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Users;
using ContainerControl.Modules.Access.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Host.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AccessSliceTests
{
    private readonly ApiFixture _fixture;

    public AccessSliceTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_live_and_ready_report_the_process_and_database()
    {
        var client = _fixture.Factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var liveBody = await live.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Contains("Healthy", liveBody, StringComparison.Ordinal);
        Assert.Contains("self", liveBody, StringComparison.Ordinal);

        var ready = await client.GetAsync("/health/ready");
        var readyBody = await ready.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Contains("Healthy", readyBody, StringComparison.Ordinal);
        Assert.Contains("database", readyBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_in_returns_permissions_and_rejects_a_missing_user()
    {
        var email = UniqueEmail("member");
        await ApiClient.CreateUserAsync(_fixture.Factory, email, PermissionCatalog.AppsRead);
        var client = _fixture.Factory.CreateClient();

        await client.SignInAsync(email, ApiClient.SamplePassword);
        var permissions = await client.ReadPermissionsAsync();
        Assert.Contains(PermissionCatalog.AppsRead, permissions);

        var session = await client.GetAsync("/auth/session");
        var sessionBody = await session.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Contains("\"signedIn\":true", sessionBody, StringComparison.Ordinal);
        Assert.Contains(PermissionCatalog.AppsRead, sessionBody, StringComparison.Ordinal);

        var rejected = await client.SendAsync(
            HttpMethod.Post,
            "/auth/login",
            new { email = UniqueEmail("missing"), password = ApiClient.SamplePassword });
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Fact]
    public async Task Self_registration_is_not_available_and_login_requires_anti_forgery()
    {
        var client = _fixture.Factory.CreateClient();

        var register = await client.SendAsync(HttpMethod.Post, "/auth/register", new { email = "a@localhost", password = "x" });
        Assert.Equal(HttpStatusCode.NotFound, register.StatusCode);

        var login = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { email = "a@localhost", password = ApiClient.SamplePassword })
        };
        var missingToken = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        var anonymous = await client.GetAsync("/me/permissions");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var session = await client.GetAsync("/auth/session");
        var sessionBody = await session.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Contains("\"signedIn\":false", sessionBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Developer_cannot_list_hosts_and_an_admin_can()
    {
        var developerEmail = UniqueEmail("developer");
        var adminEmail = UniqueEmail("admin");
        await ApiClient.CreateUserAsync(_fixture.Factory, developerEmail, PermissionCatalog.AppsRead, PermissionCatalog.AppsWrite);
        await ApiClient.CreateUserAsync(_fixture.Factory, adminEmail, PermissionCatalog.PlatformHostsManage);

        var developer = _fixture.Factory.CreateClient();
        await developer.SignInAsync(developerEmail, ApiClient.SamplePassword);
        var denied = await developer.GetAsync("/platform/hosts");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var admin = _fixture.Factory.CreateClient();
        await admin.SignInAsync(adminEmail, ApiClient.SamplePassword);
        var allowed = await admin.GetAsync("/platform/hosts");
        var body = await allowed.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("\"hosts\":", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_role_takes_effect_on_the_next_sign_in_without_restart()
    {
        var operatorEmail = UniqueEmail("operator");
        var memberEmail = UniqueEmail("member");
        await ApiClient.CreateUserAsync(
            _fixture.Factory,
            operatorEmail,
            PermissionCatalog.AccessRolesManage,
            PermissionCatalog.AccessUsersManage);
        var member = await ApiClient.CreateUserAsync(_fixture.Factory, memberEmail);

        var operatorClient = _fixture.Factory.CreateClient();
        await operatorClient.SignInAsync(operatorEmail, ApiClient.SamplePassword);
        var created = await operatorClient.SendAsync(
            HttpMethod.Post,
            "/access/roles",
            new
            {
                name = "hosts-" + Guid.NewGuid().ToString("n"),
                description = "Test role",
                permissionCodes = new[] { PermissionCatalog.PlatformHostsManage }
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var role = await created.Content.ReadFromJsonAsync<RoleBody>();
        Assert.NotNull(role);

        var assigned = await operatorClient.SendAsync(
            HttpMethod.Put,
            $"/access/users/{member.Id}/roles",
            new { roleIds = new[] { role.Id } });
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

        var memberClient = _fixture.Factory.CreateClient();
        await memberClient.SignInAsync(memberEmail, ApiClient.SamplePassword);
        var hosts = await memberClient.GetAsync("/platform/hosts");
        Assert.Equal(HttpStatusCode.OK, hosts.StatusCode);
    }

    [Fact]
    public async Task Development_sample_user_is_not_seeded_in_the_testing_environment()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var sample = await users.FindByEmailAsync(DevelopmentAccessSeeder.AdministratorEmail);
        Assert.Null(sample);
    }

    [Fact]
    public async Task Development_seed_user_can_sign_in_and_the_developer_role_cannot_manage_hosts()
    {
        var connectionString = await _fixture.CreateDatabaseAsync("containercontrol_dev");
        await using var factory = new ApiFactory(connectionString, environment: "Development");
        var client = factory.CreateClient();

        await client.SignInAsync(DevelopmentAccessSeeder.AdministratorEmail, DevelopmentAccessSeeder.AdministratorPassword);
        var adminPermissions = await client.ReadPermissionsAsync();
        Assert.Contains(PermissionCatalog.PlatformHostsManage, adminPermissions);
        var hosts = await client.GetAsync("/platform/hosts");
        Assert.Equal(HttpStatusCode.OK, hosts.StatusCode);

        var developer = factory.CreateClient();
        await developer.SignInAsync(DevelopmentAccessSeeder.DeveloperEmail, DevelopmentAccessSeeder.DeveloperPassword);
        var developerPermissions = await developer.ReadPermissionsAsync();
        Assert.DoesNotContain(PermissionCatalog.PlatformHostsManage, developerPermissions);
        var denied = await developer.GetAsync("/platform/hosts");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task First_admin_is_created_from_configuration_when_the_database_has_no_users()
    {
        var connectionString = await _fixture.CreateDatabaseAsync("containercontrol_boot");
        await using var factory = new ApiFactory(
            connectionString,
            settings: new Dictionary<string, string?>
            {
                ["BootstrapAdmin:Email"] = "first-admin@localhost",
                ["BootstrapAdmin:Password"] = "Dev-Admin-Passw0rd!",
                ["BootstrapAdmin:DisplayName"] = "First Admin"
            });
        var client = factory.CreateClient();
        await client.SignInAsync("first-admin@localhost", "Dev-Admin-Passw0rd!");
        var permissions = await client.ReadPermissionsAsync();
        Assert.Contains(PermissionCatalog.AccessUsersManage, permissions);
        Assert.Contains(PermissionCatalog.PlatformHostsManage, permissions);
        Assert.Equal(PermissionCatalog.All.Count, permissions.Count);
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():n}@localhost";

    private sealed record RoleBody(Guid Id, string Name, string? Description, IReadOnlyList<string> PermissionCodes);
}
