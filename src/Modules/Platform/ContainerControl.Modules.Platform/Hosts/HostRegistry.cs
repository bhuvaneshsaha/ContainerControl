using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Platform.Hosts;

public sealed class HostRegistry : IDockerHostLookup
{
    private readonly PlatformDbContext _db;
    private readonly IDockerEngine _engine;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditSink _audit;

    public HostRegistry(
        PlatformDbContext db,
        IDockerEngine engine,
        IClock clock,
        ICurrentUser currentUser,
        IAuditSink audit)
    {
        _db = db;
        _engine = engine;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DockerHost>> ListAsync(CancellationToken cancellationToken)
    {
        return await _db.Hosts
            .AsNoTracking()
            .OrderBy(host => host.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DockerHost?> FindEntityAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Hosts.SingleOrDefaultAsync(host => host.Id == id, cancellationToken);
    }

    public async Task<DockerHostSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var host = await _db.Hosts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return host is null ? null : new DockerHostSnapshot(host.Id, host.Name, host.Endpoint);
    }

    public async Task<(bool Ok, Guid? Id, string? Error)> RegisterAsync(
        string name,
        string endpoint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(endpoint))
        {
            return (false, null, "Enter a host name and an Engine endpoint.");
        }

        if (!EngineConnectUri.TryParse(endpoint, out _, out var endpointError))
        {
            return (false, null, endpointError);
        }

        var host = new DockerHost
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Endpoint = endpoint.Trim(),
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Hosts.Add(host);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("platform.host.registered", "docker-host", host.Id.ToString(), _currentUser.UserId), cancellationToken);
        return (true, host.Id, null);
    }

    public async Task<(bool Ok, string? Version, string? Error)> PingAsync(Guid id, CancellationToken cancellationToken)
    {
        var host = await _db.Hosts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (host is null)
        {
            return (false, null, "not-found");
        }

        try
        {
            var version = await _engine.GetVersionAsync(new DockerEndpoint(host.Endpoint), cancellationToken);
            host.EngineVersion = version.Version;
            host.LastPingAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(new AuditRecord("platform.host.pinged", "docker-host", host.Id.ToString(), _currentUser.UserId), cancellationToken);
            return (true, version.Version, null);
        }
        catch (Exception exception) when (exception is DockerEngineException or Docker.DotNet.DockerApiException or HttpRequestException)
        {
            return (false, null, "The Engine did not answer the version ping.");
        }
    }
}
