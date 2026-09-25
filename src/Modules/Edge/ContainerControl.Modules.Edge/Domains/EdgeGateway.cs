using ContainerControl.Modules.Edge.Persistence;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.SharedKernel.Hostnames;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ContainerControl.Modules.Edge.Domains;

public sealed class EdgeGateway : IEdgeGateway
{
    public const string TraefikContainerName = "cc-traefik";

    private readonly EdgeDbContext _db;
    private readonly IDockerEngine _engine;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public EdgeGateway(EdgeDbContext db, IDockerEngine engine, IConfiguration configuration, IClock clock)
    {
        _db = db;
        _engine = engine;
        _configuration = configuration;
        _clock = clock;
    }

    public string EdgeNetworkName => "edge";

    public async Task<IReadOnlyList<AllowedDomain>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Domains.AsNoTracking().OrderBy(domain => domain.Name).ToListAsync(cancellationToken);

    public async Task<(bool Ok, string? Error)> AddAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = PublicHostname.Normalize(name);
        if (normalized is null)
        {
            return (false, "Enter a DNS domain name without a wildcard.");
        }

        var exists = await _db.Domains.AnyAsync(domain => domain.Name == normalized, cancellationToken);
        if (!exists)
        {
            _db.Domains.Add(new AllowedDomain
            {
                Id = Guid.NewGuid(),
                Name = normalized,
                CreatedAtUtc = _clock.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return (true, null);
    }

    public async Task<bool> HostnameAllowedAsync(string? hostname, CancellationToken cancellationToken)
    {
        if (PublicHostname.Normalize(hostname) is null)
        {
            return false;
        }

        var domains = await _db.Domains.AsNoTracking().Select(domain => domain.Name).ToListAsync(cancellationToken);
        return domains.Any(domain => PublicHostname.IsUnder(hostname, domain));
    }

    public async Task EnsureEdgeAsync(string dockerEndpoint, CancellationToken cancellationToken)
    {
        var endpoint = new DockerEndpoint(dockerEndpoint);
        await _engine.EnsureNetworkAsync(endpoint, EdgeNetworkName, cancellationToken);
        var existing = await _engine.FindContainerIdByNameAsync(endpoint, TraefikContainerName, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var image = _configuration["Edge:Image"] ?? "traefik:v3.6";
        var httpPort = _configuration["Edge:HttpPort"] ?? "80";
        var httpsPort = _configuration["Edge:HttpsPort"] ?? "443";
        var socket = SocketPath(dockerEndpoint);
        await _engine.PullImageAsync(endpoint, image, null, cancellationToken);
        var command = new List<string>
        {
            "--api.insecure=false",
            "--ping=true",
            "--providers.docker=true",
            "--providers.docker.exposedbydefault=false",
            "--providers.docker.network=edge",
            "--entrypoints.web.address=:80",
            "--entrypoints.websecure.address=:443"
        };
        var email = AcmeEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            command.Add("--certificatesresolvers.le.acme.email=" + email);
            command.Add("--certificatesresolvers.le.acme.storage=/letsencrypt/acme.json");
            command.Add("--certificatesresolvers.le.acme.httpchallenge=true");
            command.Add("--certificatesresolvers.le.acme.httpchallenge.entrypoint=web");
            command.Add("--entrypoints.web.http.redirections.entrypoint.to=websecure");
            command.Add("--entrypoints.web.http.redirections.entrypoint.scheme=https");
            command.Add("--entrypoints.web.http.redirections.entrypoint.permanent=true");
        }

        var id = await _engine.CreateContainerAsync(endpoint, new ContainerPlan(
            TraefikContainerName,
            image,
            command,
            new Dictionary<string, string> { ["cc.role"] = "traefik" },
            new Dictionary<string, string>(),
            EdgeNetworkName,
            "traefik",
            [],
            [$"{socket}:/var/run/docker.sock:ro", "cc-traefik-acme:/letsencrypt"],
            new Dictionary<string, string>
            {
                ["80/tcp"] = httpPort,
                ["443/tcp"] = httpsPort
            },
            "unless-stopped"), cancellationToken);
        await _engine.StartContainerAsync(endpoint, id, cancellationToken);
    }

    public IReadOnlyDictionary<string, string> LabelsFor(string routerName, string hostname, int port)
    {
        var rule = PublicHostname.TraefikHostRule(hostname);
        if (rule is null)
        {
            throw new ArgumentException("The hostname is not a DNS name.", nameof(hostname));
        }

        var router = "traefik.http.routers." + routerName;
        var labels = new Dictionary<string, string>
        {
            ["traefik.enable"] = "true",
            ["traefik.docker.network"] = EdgeNetworkName,
            [router + ".rule"] = rule,
            [router + ".entrypoints"] = AcmeConfigured() ? "websecure" : "web",
            ["traefik.http.services." + routerName + ".loadbalancer.server.port"] = port.ToString()
        };
        if (AcmeConfigured())
        {
            labels[router + ".tls"] = "true";
            labels[router + ".tls.certresolver"] = "le";
        }

        return labels;
    }

    private bool AcmeConfigured() => !string.IsNullOrWhiteSpace(AcmeEmail());

    private string? AcmeEmail() => _configuration is null ? null : _configuration["Edge:AcmeEmail"];

    private static string SocketPath(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "unix")
        {
            return uri.AbsolutePath;
        }

        return "/var/run/docker.sock";
    }
}
