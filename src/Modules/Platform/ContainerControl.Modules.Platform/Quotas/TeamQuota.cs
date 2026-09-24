namespace ContainerControl.Modules.Platform.Quotas;

public sealed class TeamQuota
{
    public Guid TeamId { get; set; }

    public long CpuMillicores { get; set; }

    public long MemoryBytes { get; set; }

    public long StorageBytes { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class HostCapacityReading
{
    public Guid HostId { get; set; }

    public long CpuCount { get; set; }

    public long MemoryBytes { get; set; }

    public long? StorageBytes { get; set; }

    public DateTimeOffset ReadAtUtc { get; set; }
}

public interface ITeamQuotaLookup
{
    Task<ServiceResources?> FindAsync(Guid teamId, CancellationToken cancellationToken);
}
