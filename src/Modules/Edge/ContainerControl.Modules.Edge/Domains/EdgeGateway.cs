using ContainerControl.Modules.Edge.Persistence;
using ContainerControl.Modules.Platform.Engine;
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
        var normalized = name.Trim().Trim('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains(' ') || normalized.Contains('*'))
        {
            return (false, "Enter a domain name without a wildcard.");
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
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        var host = hostname.Trim().Trim('.').ToLowerInvariant();
        var domains = await _db.Domains.AsNoTracking().Select(domain => domain.Name).ToListAsync(cancellationToken);
        return domains.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal));
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
        await _engine.PullImageAsync(endpoint, image, cancellationToken);
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
        var email = _configuration["Edge:AcmeEmail"];
        if (!string.IsNullOrWhiteSpace(email))
        {
            command.Add("--certificatesresolvers.le.acme.email=" + email);
            command.Add("--certificatesresolvers.le.acme.storage=/letsencrypt/acme.json");
            command.Add("--certificatesresolvers.le.acme.httpchallenge=true");
            command.Add("--certificatesresolvers.le.acme.httpchallenge.entrypoint=web");
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

    public IReadOnlyDictionary<string, string> LabelsFor(string routerName, string hostname, int port) =>
        new Dictionary<string, string>
        {
            ["traefik.enable"] = "true",
            ["traefik.docker.network"] = EdgeNetworkName,
            ["traefik.http.routers." + routerName + ".rule"] = "Host(`" + hostname + "`)",
            ["traefik.http.routers." + routerName + ".entrypoints"] = "web",
            ["traefik.http.services." + routerName + ".loadbalancer.server.port"] = port.ToString()
        };

    private static string SocketPath(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "unix")
        {
            return uri.AbsolutePath;
        }

        return "/var/run/docker.sock";
    }
}
