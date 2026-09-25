using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Platform.Engine;
using Microsoft.Extensions.Configuration;

namespace ContainerControl.Host.IntegrationTests;

public sealed class EdgeTlsLabelTests
{
    [Fact]
    public void Acme_email_stamps_websecure_on_the_existing_host_rule()
    {
        var gateway = Gateway("ops@example.com");
        var labels = gateway.LabelsFor("web", "https://My-App.apps.example.com/welcome", 80);

        Assert.Equal("Host(`my-app.apps.example.com`)", labels["traefik.http.routers.web.rule"]);
        Assert.Equal("websecure", labels["traefik.http.routers.web.entrypoints"]);
        Assert.Equal("true", labels["traefik.http.routers.web.tls"]);
        Assert.Equal("le", labels["traefik.http.routers.web.tls.certresolver"]);
        Assert.Equal("80", labels["traefik.http.services.web.loadbalancer.server.port"]);
    }

    [Fact]
    public void Blank_acme_email_stays_on_web()
    {
        var gateway = Gateway("  ");
        var labels = gateway.LabelsFor("web", "web.apps.example.com", 8080);

        Assert.Equal("Host(`web.apps.example.com`)", labels["traefik.http.routers.web.rule"]);
        Assert.Equal("web", labels["traefik.http.routers.web.entrypoints"]);
        Assert.False(labels.ContainsKey("traefik.http.routers.web.tls"));
        Assert.False(labels.ContainsKey("traefik.http.routers.web.tls.certresolver"));
    }

    [Fact]
    public void Acme_email_does_not_bypass_a_widened_host_rule()
    {
        var gateway = Gateway("ops@example.com");
        var injection = "evil.com`)||Host(`web.apps.example.com";

        Assert.Null(ContainerControl.SharedKernel.Hostnames.PublicHostname.TraefikHostRule(injection));
        var error = Assert.Throws<ArgumentException>(() => gateway.LabelsFor("web", injection, 80));
        Assert.Equal("hostname", error.ParamName);
    }

    [Fact]
    public async Task Prepare_redirects_http_when_acme_email_is_set()
    {
        var engine = new RecordingEngine();
        var gateway = new EdgeGateway(null!, engine, Configuration("ops@example.com"), null!);

        await gateway.EnsureEdgeAsync("unix:///var/run/docker.sock", CancellationToken.None);

        var command = engine.Created!.Command!;
        Assert.Contains("--certificatesresolvers.le.acme.email=ops@example.com", command);
        Assert.Contains("--certificatesresolvers.le.acme.httpchallenge.entrypoint=web", command);
        Assert.Contains("--entrypoints.web.http.redirections.entrypoint.to=websecure", command);
        Assert.Contains("--entrypoints.web.http.redirections.entrypoint.scheme=https", command);
        Assert.Contains("--entrypoints.web.http.redirections.entrypoint.permanent=true", command);
        Assert.Contains("/var/run/docker.sock:/var/run/docker.sock:ro", engine.Created.Binds);
    }

    [Fact]
    public async Task Prepare_without_acme_email_does_not_redirect()
    {
        var engine = new RecordingEngine();
        var gateway = new EdgeGateway(null!, engine, Configuration(null), null!);

        await gateway.EnsureEdgeAsync("unix:///var/run/docker.sock", CancellationToken.None);

        var command = engine.Created!.Command!;
        Assert.Contains("--entrypoints.web.address=:80", command);
        Assert.Contains("--entrypoints.websecure.address=:443", command);
        Assert.DoesNotContain(command, line => line.Contains("certificatesresolvers", StringComparison.Ordinal));
        Assert.DoesNotContain(command, line => line.Contains("redirections", StringComparison.Ordinal));
        Assert.Contains("/var/run/docker.sock:/var/run/docker.sock:ro", engine.Created.Binds);
    }

    private static EdgeGateway Gateway(string? email) =>
        new(null!, null!, Configuration(email), null!);

    private static IConfiguration Configuration(string? email)
    {
        var values = new Dictionary<string, string?>();
        if (email is not null)
        {
            values["Edge:AcmeEmail"] = email;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class RecordingEngine : IDockerEngine
    {
        public ContainerPlan? Created { get; private set; }

        public Task EnsureNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<string?> FindContainerIdByNameAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task PullImageAsync(DockerEndpoint endpoint, string image, ImagePullAuth? auth, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<string> CreateContainerAsync(DockerEndpoint endpoint, ContainerPlan plan, CancellationToken cancellationToken)
        {
            Created = plan;
            return Task.FromResult("traefik");
        }

        public Task StartContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<EngineVersion> GetVersionAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListContainerIdsAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RemoveNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ExtractArchiveAsync(DockerEndpoint endpoint, string containerId, string destinationPath, Stream archive, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task StopContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RemoveContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EngineContainer>> ListByLabelAsync(DockerEndpoint endpoint, string label, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ExecAsync(DockerEndpoint endpoint, string containerId, IReadOnlyList<string> command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ReadLogsAsync(DockerEndpoint endpoint, string containerId, int tail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task FollowLogsAsync(DockerEndpoint endpoint, string containerId, int tail, IProgress<string> progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ContainerSample> ReadStatsAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> ReadHealthStatusAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HostCapacity> ReadCapacityAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
