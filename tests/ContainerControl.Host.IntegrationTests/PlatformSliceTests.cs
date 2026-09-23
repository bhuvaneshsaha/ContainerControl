using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Access.Domain.Teams;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.Modules.Applications.Persistence;
using ContainerControl.Modules.Platform.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Host.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PlatformSliceTests
{
    private const string EngineEndpoint = "unix:///var/run/docker.sock";

    private readonly ApiFixture _fixture;

    public PlatformSliceTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Version_ping_does_not_create_a_container()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var admin = factory.CreateClient();
        using var developer = factory.CreateClient();
        var adminUser = await ApiClient.CreateUserAsync(factory, $"admin-{Guid.NewGuid():N}@localhost", PermissionCatalog.PlatformHostsManage);
        var developerUser = await ApiClient.CreateUserAsync(factory, $"dev-{Guid.NewGuid():N}@localhost", PermissionCatalog.AppsRead);
        await admin.SignInAsync(adminUser.Email!, ApiClient.SamplePassword);
        await developer.SignInAsync(developerUser.Email!, ApiClient.SamplePassword);

        await using var scope = factory.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IDockerEngine>();
        var before = await engine.ListContainerIdsAsync(new DockerEndpoint(EngineEndpoint), CancellationToken.None);

        var denied = await developer.SendAsync(HttpMethod.Post, "/platform/hosts", new { name = "local", endpoint = EngineEndpoint });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var created = await admin.SendAsync(HttpMethod.Post, "/platform/hosts", new { name = "local-" + Guid.NewGuid().ToString("N")[..8], endpoint = EngineEndpoint });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var host = await created.Content.ReadFromJsonAsync<IdBody>();
        var ping = await admin.SendAsync(HttpMethod.Post, $"/platform/hosts/{host!.Id}/ping");
        Assert.Equal(HttpStatusCode.OK, ping.StatusCode);
        var version = await ping.Content.ReadFromJsonAsync<VersionBody>();
        Assert.False(string.IsNullOrWhiteSpace(version!.EngineVersion));

        var after = await engine.ListContainerIdsAsync(new DockerEndpoint(EngineEndpoint), CancellationToken.None);
        Assert.Equal(before.OrderBy(id => id), after.OrderBy(id => id));
    }

    [Fact]
    public async Task Secret_value_is_written_to_infisical_and_not_to_postgresql()
    {
        const string secretValue = "super-secret-value-9f3a";
        await using var infisical = new InfisicalStandIn();
        await using var factory = new ApiFactory(_fixture.ConnectionString, settings: new Dictionary<string, string?>
        {
            ["Infisical:SiteUrl"] = infisical.BaseUrl,
            ["Infisical:Environments:dev:ClientId"] = "client",
            ["Infisical:Environments:dev:ClientSecret"] = "secret",
            ["Infisical:Environments:dev:ProjectId"] = "project"
        });
        using var client = factory.CreateClient();
        var user = await ApiClient.CreateUserAsync(
            factory,
            $"secrets-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.SecretsRead,
            PermissionCatalog.SecretsManage);
        var teamId = await AddMemberAsync(factory, user.Id);
        await client.SignInAsync(user.Email!, ApiClient.SamplePassword);

        var save = await client.SendAsync(HttpMethod.Post, "/secrets", new
        {
            teamId,
            environment = "dev",
            name = "db-password",
            injectionMode = "env",
            value = secretValue
        });
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        Assert.Equal(secretValue, infisical.Read("dev", "db-password"));

        var list = await client.GetAsync($"/secrets?teamId={teamId}&environment=dev");
        var body = await list.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("db-password", body, StringComparison.Ordinal);
        Assert.Contains("env", body, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, body, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        var row = await db.Secrets.SingleAsync(secret => secret.TeamId == teamId && secret.Name == "db-password");
        var stored = row.Name + row.Path + row.Environment + row.InjectionMode + row.Id;
        Assert.DoesNotContain(secretValue, stored, StringComparison.Ordinal);
        Assert.Equal("env", row.InjectionMode);
    }

    [Fact]
    public async Task Deploy_start_stop_network_policy_route_logs_and_rollback()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString, settings: new Dictionary<string, string?>
        {
            ["Edge:HttpPort"] = "18080",
            ["Edge:HttpsPort"] = "18443"
        });
        using var admin = factory.CreateClient();
        using var developer = factory.CreateClient();
        var adminUser = await ApiClient.CreateUserAsync(
            factory,
            $"ops-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.PlatformHostsManage,
            PermissionCatalog.EdgeDnsManage);
        var developerUser = await ApiClient.CreateUserAsync(
            factory,
            $"ship-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite,
            PermissionCatalog.DeployExecute,
            PermissionCatalog.DeployRollback,
            PermissionCatalog.RuntimeControl,
            PermissionCatalog.RuntimeLogsRead,
            PermissionCatalog.RuntimeStatsRead);
        var teamId = await AddMemberAsync(factory, developerUser.Id);
        await admin.SignInAsync(adminUser.Email!, ApiClient.SamplePassword);
        await developer.SignInAsync(developerUser.Email!, ApiClient.SamplePassword);

        var hostResponse = await admin.SendAsync(HttpMethod.Post, "/platform/hosts", new { name = "engine-" + Guid.NewGuid().ToString("N")[..8], endpoint = EngineEndpoint });
        hostResponse.EnsureSuccessStatusCode();
        var host = await hostResponse.Content.ReadFromJsonAsync<IdBody>();
        var domain = await admin.SendAsync(HttpMethod.Post, "/edge/domains", new { name = "apps.localhost" });
        Assert.Equal(HttpStatusCode.NoContent, domain.StatusCode);

        Guid? singleId = null;
        Guid? firstApp = null;
        Guid? secondApp = null;
        try
        {
            singleId = await CreateApp(developer, teamId, host!.Id, new
            {
                teamId,
                hostId = host.Id,
                name = "single",
                environment = "dev",
                image = "busybox:1.36.1",
                command = new[] { "sh", "-c", "echo version-one; sleep 300" },
                internalPort = (int?)null,
                hostname = (string?)null,
                exposed = false
            });
            var deployed = await developer.SendAsync(HttpMethod.Post, $"/apps/{singleId}/deploy");
            Assert.Equal(HttpStatusCode.OK, deployed.StatusCode);
            await WaitForStatus(developer, singleId.Value, "running");

            var stopped = await developer.SendAsync(HttpMethod.Post, $"/apps/{singleId}/stop");
            Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
            await WaitForStatus(developer, singleId.Value, "stopped");
            var started = await developer.SendAsync(HttpMethod.Post, $"/apps/{singleId}/start");
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            await WaitForStatus(developer, singleId.Value, "running");

            var logs = await developer.GetFromJsonAsync<LogBody>($"/apps/{singleId}/logs");
            Assert.Contains("version-one", logs!.Text, StringComparison.Ordinal);
            var stats = await developer.GetFromJsonAsync<StatsBody>($"/apps/{singleId}/stats");
            Assert.NotEmpty(stats!.Services);
            Assert.True(stats.Services[0].MemoryBytes > 0);

            var updated = await developer.SendAsync(HttpMethod.Put, $"/apps/{singleId}", new
            {
                image = "busybox:1.36.1",
                command = new[] { "sh", "-c", "echo version-two; sleep 300" },
                exposed = false
            });
            Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
            (await developer.SendAsync(HttpMethod.Post, $"/apps/{singleId}/deploy")).EnsureSuccessStatusCode();
            await WaitForLog(developer, singleId.Value, "version-two");
            (await developer.SendAsync(HttpMethod.Post, $"/apps/{singleId}/rollback")).EnsureSuccessStatusCode();
            await WaitForLog(developer, singleId.Value, "version-one");

            await using var scope = factory.Services.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IDockerEngine>();
            var beforePolicy = await engine.ListContainerIdsAsync(new DockerEndpoint(EngineEndpoint), CancellationToken.None);
            var privileged = await developer.SendAsync(HttpMethod.Post, "/apps", new
            {
                teamId,
                hostId = host.Id,
                name = "bad-priv",
                environment = "dev",
                composeYaml = "services:\n  web:\n    image: busybox:1.36.1\n    privileged: true\n"
            });
            var badId = await ReadId(privileged);
            var rejected = await developer.SendAsync(HttpMethod.Post, $"/apps/{badId}/deploy");
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var bind = await developer.SendAsync(HttpMethod.Put, $"/apps/{badId}", new
            {
                composeYaml = "services:\n  web:\n    image: busybox:1.36.1\n    volumes:\n      - /tmp:/tmp\n"
            });
            bind.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await developer.SendAsync(HttpMethod.Post, $"/apps/{badId}/deploy")).StatusCode);
            (await developer.SendAsync(HttpMethod.Put, $"/apps/{badId}", new
            {
                composeYaml = "services:\n  web:\n    image: postgres:17-alpine\n"
            })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await developer.SendAsync(HttpMethod.Post, $"/apps/{badId}/deploy")).StatusCode);
            var afterPolicy = await engine.ListContainerIdsAsync(new DockerEndpoint(EngineEndpoint), CancellationToken.None);
            Assert.Equal(beforePolicy.OrderBy(id => id), afterPolicy.OrderBy(id => id));

            firstApp = await CreateApp(developer, teamId, host.Id, new
            {
                teamId,
                hostId = host.Id,
                name = "web-cache",
                environment = "dev",
                composeYaml = """
                    services:
                      web:
                        image: busybox:1.36.1
                        command: ["sleep", "300"]
                      cache:
                        image: busybox:1.36.1
                        command: ["sleep", "300"]
                    """
            });
            (await developer.SendAsync(HttpMethod.Post, $"/apps/{firstApp}/deploy")).EnsureSuccessStatusCode();
            secondApp = await CreateApp(developer, teamId, host.Id, new
            {
                teamId,
                hostId = host.Id,
                name = "other-cache",
                environment = "dev",
                composeYaml = """
                    services:
                      web:
                        image: busybox:1.36.1
                        command: ["sleep", "300"]
                      cache:
                        image: busybox:1.36.1
                        command: ["sleep", "300"]
                    """
            });
            (await developer.SendAsync(HttpMethod.Post, $"/apps/{secondApp}/deploy")).EnsureSuccessStatusCode();

            var firstWeb = await FindAsync(engine, firstApp.Value, "web");
            var firstCache = await FindAsync(engine, firstApp.Value, "cache");
            var secondCache = await FindAsync(engine, secondApp.Value, "cache");
            var resolved = await engine.ExecAsync(new DockerEndpoint(EngineEndpoint), firstWeb.Id, ["nslookup", "cache"], CancellationToken.None);
            Assert.Contains(firstCache.IpAddress!, resolved, StringComparison.Ordinal);
            Assert.DoesNotContain(secondCache.IpAddress!, resolved, StringComparison.Ordinal);

            var prepare = await admin.SendAsync(HttpMethod.Post, $"/platform/hosts/{host.Id}/prepare");
            Assert.Equal(HttpStatusCode.NoContent, prepare.StatusCode);
            var exposedId = await CreateApp(developer, teamId, host.Id, new
            {
                teamId,
                hostId = host.Id,
                name = "public-web",
                environment = "dev",
                image = "traefik/whoami:v1.10.3",
                internalPort = 80,
                hostname = "web.apps.localhost",
                exposed = true
            });
            (await developer.SendAsync(HttpMethod.Post, $"/apps/{exposedId}/deploy")).EnsureSuccessStatusCode();
            var hiddenId = await CreateApp(developer, teamId, host.Id, new
            {
                teamId,
                hostId = host.Id,
                name = "hidden-web",
                environment = "dev",
                image = "traefik/whoami:v1.10.3",
                internalPort = 80,
                hostname = "hidden.apps.localhost",
                exposed = false
            });
            (await developer.SendAsync(HttpMethod.Post, $"/apps/{hiddenId}/deploy")).EnsureSuccessStatusCode();

            using var http = new HttpClient();
            var routed = await WaitForRoute(http, "web.apps.localhost");
            Assert.Contains("web.apps.localhost", routed, StringComparison.Ordinal);
            var hidden = await http.SendAsync(Request("hidden.apps.localhost"));
            var hiddenBody = await hidden.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Hostname: hidden.apps.localhost", hiddenBody, StringComparison.Ordinal);
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IDockerEngine>();
            var containers = await engine.ListByLabelAsync(new DockerEndpoint(EngineEndpoint), "cc.managed=true", CancellationToken.None);
            foreach (var container in containers)
            {
                await engine.RemoveContainerAsync(new DockerEndpoint(EngineEndpoint), container.Id, CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task Webhook_requires_deploy_execute()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var allowed = factory.CreateClient();
        using var blocked = factory.CreateClient();
        var allowedUser = await ApiClient.CreateUserAsync(
            factory,
            $"ci-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AccessTokensManage,
            PermissionCatalog.AppsRead,
            PermissionCatalog.AppsWrite,
            PermissionCatalog.DeployExecute,
            PermissionCatalog.PlatformHostsManage);
        var blockedUser = await ApiClient.CreateUserAsync(
            factory,
            $"ci-no-{Guid.NewGuid():N}@localhost",
            PermissionCatalog.AccessTokensManage,
            PermissionCatalog.AppsRead);
        var teamId = await AddMemberAsync(factory, allowedUser.Id);
        await AddMemberAsync(factory, blockedUser.Id, teamId);
        await allowed.SignInAsync(allowedUser.Email!, ApiClient.SamplePassword);
        await blocked.SignInAsync(blockedUser.Email!, ApiClient.SamplePassword);

        var hostResponse = await allowed.SendAsync(HttpMethod.Post, "/platform/hosts", new { name = "ci-" + Guid.NewGuid().ToString("N")[..8], endpoint = EngineEndpoint });
        var host = await hostResponse.Content.ReadFromJsonAsync<IdBody>();
        var appId = await CreateApp(allowed, teamId, host!.Id, new
        {
            teamId,
            hostId = host.Id,
            name = "hook",
            environment = "dev",
            image = "busybox:1.36.1",
            command = new[] { "sleep", "30" },
            exposed = false
        });

        var allowedToken = await Issue(allowed);
        var blockedToken = await Issue(blocked);
        using var hook = factory.CreateClient();
        var denied = await SendHook(hook, blockedToken, appId);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var accepted = await SendHook(hook, allowedToken, appId);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IDockerEngine>();
        var containers = await engine.ListByLabelAsync(new DockerEndpoint(EngineEndpoint), "cc.application=" + appId, CancellationToken.None);
        foreach (var container in containers)
        {
            await engine.RemoveContainerAsync(new DockerEndpoint(EngineEndpoint), container.Id, CancellationToken.None);
        }
    }

    private static async Task<string> Issue(HttpClient client)
    {
        var response = await client.SendAsync(HttpMethod.Post, "/access/tokens", new { name = "ci" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenBody>();
        return body!.Token;
    }

    private static Task<HttpResponseMessage> SendHook(HttpClient client, string token, Guid appId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/delivery/webhook")
        {
            Content = JsonContent.Create(new { applicationId = appId })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<Guid> CreateApp(HttpClient client, Guid teamId, Guid hostId, object body)
    {
        var response = await client.SendAsync(HttpMethod.Post, "/apps", body);
        response.EnsureSuccessStatusCode();
        return await ReadId(response);
    }

    private static async Task<Guid> ReadId(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<IdBody>();
        return body!.Id;
    }

    private static async Task WaitForStatus(HttpClient client, Guid appId, string status)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var app = await client.GetFromJsonAsync<AppBody>($"/apps/{appId}");
            if (app?.Status == status)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Application did not reach {status}.");
    }

    private static async Task WaitForLog(HttpClient client, Guid appId, string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var logs = await client.GetFromJsonAsync<LogBody>($"/apps/{appId}/logs");
            if (logs?.Text.Contains(text, StringComparison.Ordinal) == true)
            {
                return;
            }

            await Task.Delay(300);
        }

        throw new TimeoutException($"Logs did not contain {text}.");
    }

    private static async Task<string> WaitForRoute(HttpClient http, string host)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        string last = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.SendAsync(Request(host));
                last = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode && last.Contains(host, StringComparison.Ordinal))
                {
                    return last;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Traefik did not route the hostname. Last body: " + last);
    }

    private static HttpRequestMessage Request(string host)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:18080/");
        request.Headers.Host = host;
        return request;
    }

    private static async Task<EngineContainer> FindAsync(IDockerEngine engine, Guid appId, string service)
    {
        var containers = await engine.ListByLabelAsync(new DockerEndpoint(EngineEndpoint), "cc.application=" + appId, CancellationToken.None);
        return containers.Single(container => container.Name.EndsWith("-" + service, StringComparison.Ordinal));
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
    private sealed record VersionBody(string EngineVersion);
    private sealed record LogBody(string Text);
    private sealed record StatsBody(IReadOnlyList<ServiceStat> Services);
    private sealed record ServiceStat(string Service, double CpuPercent, long MemoryBytes);
    private sealed record AppBody(string Status);
    private sealed record TokenBody(Guid Id, string Token);
}

file sealed class InfisicalStandIn : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();

    public InfisicalStandIn()
    {
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _ = Task.Run(Loop);
    }

    public string BaseUrl { get; }

    public string? Read(string environment, string name)
    {
        _values.TryGetValue(environment + "/" + name, out var value);
        return value;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        _listener.Close();
        await Task.CompletedTask;
    }

    private async Task Loop()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (_stop.IsCancellationRequested || !_listener.IsListening)
            {
                break;
            }

            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/api/v1/auth/universal-auth/login", StringComparison.Ordinal))
            {
                await Write(context, """{"accessToken":"test-token"}""");
                continue;
            }

            var name = Uri.UnescapeDataString(path.Split('/').LastOrDefault() ?? string.Empty);
            var environment = "dev";
            if (context.Request.Url?.Query is { Length: > 1 } query)
            {
                var parsed = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query);
                if (parsed.TryGetValue("environment", out var values) && values.Count > 0)
                {
                    environment = values[0]!;
                }
            }

            var key = environment + "/" + name;
            if (context.Request.HttpMethod == "GET")
            {
                _values.TryGetValue(key, out var value);
                await Write(context, "{\"secret\":{\"secretValue\":" + JsonSerializer.Serialize(value) + "}}");
            }
            else if (context.Request.HttpMethod == "DELETE")
            {
                _values.Remove(key);
                await Write(context, "{}");
            }
            else
            {
                using var document = await JsonDocument.ParseAsync(context.Request.InputStream);
                var env = document.RootElement.GetProperty("environment").GetString() ?? "dev";
                var value = document.RootElement.GetProperty("secretValue").GetString() ?? string.Empty;
                _values[env + "/" + Uri.UnescapeDataString(name)] = value;
                await Write(context, "{}");
            }
        }
    }

    private static async Task Write(HttpListenerContext context, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
