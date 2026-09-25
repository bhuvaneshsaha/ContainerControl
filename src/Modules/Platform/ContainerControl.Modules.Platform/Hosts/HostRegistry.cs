using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace ContainerControl.Modules.Platform.Hosts;

public sealed class HostRegistry : IDockerHostLookup
{
    private readonly PlatformDbContext _db;
    private readonly IDockerEngine _engine;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditSink _audit;
    private readonly IHostEnvironment? _environment;
    private readonly IEngineCertificateLoader? _certificates;

    public HostRegistry(
        PlatformDbContext db,
        IDockerEngine engine,
        IClock clock,
        ICurrentUser currentUser,
        IAuditSink audit,
        IHostEnvironment? environment = null,
        IEngineCertificateLoader? certificates = null)
    {
        _db = db;
        _engine = engine;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _environment = environment;
        _certificates = certificates;
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
        CancellationToken cancellationToken,
        string? clientCertRef = null,
        string? clientKeyRef = null,
        string? caRef = null)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(endpoint))
        {
            return (false, null, "Enter a host name and an Engine endpoint.");
        }

        if (!EngineConnectUri.TryParse(endpoint, out var uri, out var endpointError) || uri is null)
        {
            return (false, null, endpointError);
        }

        if (!EngineTls.TryReference(clientCertRef, out var cert, out var certError))
        {
            return (false, null, certError ?? EngineTls.MaterialMessage);
        }

        if (!EngineTls.TryReference(clientKeyRef, out var key, out var keyError))
        {
            return (false, null, keyError ?? EngineTls.MaterialMessage);
        }

        if (!EngineTls.TryReference(caRef, out var ca, out var caError))
        {
            return (false, null, caError ?? EngineTls.MaterialMessage);
        }

        string? storedCert = cert?.Stored;
        string? storedKey = key?.Stored;
        string? storedCa = ca?.Stored;
        if (uri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase))
        {
            var ready = await RequireTcpMaterialAsync(cert, key, ca, cancellationToken);
            if (!ready.Ok)
            {
                return (false, null, ready.Error);
            }
        }

        var host = new DockerHost
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Endpoint = endpoint.Trim(),
            ClientCertRef = storedCert,
            ClientKeyRef = storedKey,
            CaRef = storedCa,
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Hosts.Add(host);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("platform.host.registered", "docker-host", host.Id.ToString(), _currentUser.UserId), cancellationToken);
        return (true, host.Id, null);
    }

    private async Task<(bool Ok, string? Error)> RequireTcpMaterialAsync(
        EngineTlsReference? cert,
        EngineTlsReference? key,
        EngineTlsReference? ca,
        CancellationToken cancellationToken)
    {
        var any = cert is not null || key is not null || ca is not null;
        var all = cert is not null && key is not null && ca is not null;
        if (!all)
        {
            if (_environment?.IsDevelopment() == true && !any)
            {
                return (true, null);
            }

            return (false, EngineTls.CleartextMessage);
        }

        if (_certificates is null)
        {
            return (false, EngineTls.UnreadableMessage);
        }

        try
        {
            using var material = await _certificates.LoadAsync(cert!.Stored, key!.Stored, ca!.Stored, cancellationToken);
            if (!material.Client.HasPrivateKey)
            {
                return (false, EngineTls.UnreadableMessage);
            }
        }
        catch (DockerEngineException)
        {
            return (false, EngineTls.UnreadableMessage);
        }
        catch (Exception)
        {
            return (false, EngineTls.UnreadableMessage);
        }

        return (true, null);
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
        catch (DockerEngineException exception) when (EngineTls.IsOperatorMessage(exception.Message))
        {
            return (false, null, exception.Message);
        }
        catch (Exception exception) when (exception is DockerEngineException or Docker.DotNet.DockerApiException or HttpRequestException)
        {
            return (false, null, "The Engine did not answer the version ping.");
        }
    }
}
