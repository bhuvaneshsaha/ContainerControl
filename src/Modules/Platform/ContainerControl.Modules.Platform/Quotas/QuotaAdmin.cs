using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Platform.Quotas;

public sealed record CapacityRow(
    Guid HostId,
    string HostName,
    long? CpuCount,
    long? MemoryBytes,
    long? StorageBytes,
    DateTimeOffset? ReadAtUtc);

public sealed class QuotaAdmin : ITeamQuotaLookup
{
    private readonly PlatformDbContext _db;
    private readonly IDockerEngine _engine;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditSink _audit;

    public QuotaAdmin(PlatformDbContext db, IDockerEngine engine, IClock clock, ICurrentUser currentUser, IAuditSink audit)
    {
        _db = db;
        _engine = engine;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ServiceResources?> FindAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var quota = await _db.Quotas.AsNoTracking().SingleOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        return quota is null ? null : new ServiceResources(quota.CpuMillicores, quota.MemoryBytes, quota.StorageBytes);
    }

    public async Task<IReadOnlyList<TeamQuota>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Quotas.AsNoTracking().OrderBy(quota => quota.TeamId).ToListAsync(cancellationToken);

    public async Task<(bool Ok, string? Error)> SaveAsync(
        Guid teamId,
        long cpuMillicores,
        long memoryBytes,
        long storageBytes,
        CancellationToken cancellationToken)
    {
        if (teamId == Guid.Empty || cpuMillicores <= 0 || memoryBytes <= 0 || storageBytes <= 0)
        {
            return (false, "Enter a team and a CPU, memory, and storage quota greater than zero.");
        }

        var quota = await _db.Quotas.SingleOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (quota is null)
        {
            quota = new TeamQuota { TeamId = teamId };
            _db.Quotas.Add(quota);
        }

        quota.CpuMillicores = cpuMillicores;
        quota.MemoryBytes = memoryBytes;
        quota.StorageBytes = storageBytes;
        quota.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("platform.quota.saved", "team", teamId.ToString(), _currentUser.UserId), cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<CapacityRow>> ListCapacityAsync(CancellationToken cancellationToken)
    {
        var hosts = await _db.Hosts.AsNoTracking().OrderBy(host => host.Name).ToListAsync(cancellationToken);
        var readings = await _db.Capacity.AsNoTracking().ToDictionaryAsync(reading => reading.HostId, cancellationToken);
        return hosts.Select(host =>
        {
            readings.TryGetValue(host.Id, out var reading);
            return new CapacityRow(host.Id, host.Name, reading?.CpuCount, reading?.MemoryBytes, reading?.StorageBytes, reading?.ReadAtUtc);
        }).ToArray();
    }

    public async Task<(bool Ok, string? Error)> RecordCapacityAsync(Guid hostId, CancellationToken cancellationToken)
    {
        var host = await _db.Hosts.SingleOrDefaultAsync(item => item.Id == hostId, cancellationToken);
        if (host is null)
        {
            return (false, "not-found");
        }

        try
        {
            var sample = await _engine.ReadCapacityAsync(new DockerEndpoint(host.Endpoint), cancellationToken);
            var reading = await _db.Capacity.SingleOrDefaultAsync(item => item.HostId == hostId, cancellationToken);
            if (reading is null)
            {
                reading = new HostCapacityReading { HostId = hostId };
                _db.Capacity.Add(reading);
            }

            reading.CpuCount = sample.CpuCount;
            reading.MemoryBytes = sample.MemoryBytes;
            reading.StorageBytes = sample.StorageBytes;
            reading.ReadAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(new AuditRecord("platform.capacity.recorded", "docker-host", hostId.ToString(), _currentUser.UserId), cancellationToken);
            return (true, null);
        }
        catch (DockerEngineException exception) when (EngineTls.IsOperatorMessage(exception.Message))
        {
            return (false, exception.Message);
        }
        catch (Exception exception) when (exception is DockerEngineException or Docker.DotNet.DockerApiException or HttpRequestException or IOException)
        {
            return (false, "The Docker host could not be read.");
        }
    }
}
