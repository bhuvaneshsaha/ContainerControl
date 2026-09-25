namespace ContainerControl.Modules.Applications.Workloads;

public sealed class ContainerApp
{
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    public Guid HostId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Environment { get; set; } = "dev";

    public string? Image { get; set; }

    public string? CommandJson { get; set; }

    public string? ComposeYaml { get; set; }

    public int? InternalPort { get; set; }

    public string? Hostname { get; set; }

    public bool Exposed { get; set; }

    public bool RequiresApproval { get; set; }

    public bool AllowDatabaseImages { get; set; }

    public string Status { get; set; } = "registered";

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed record WorkloadSnapshot(
    Guid Id,
    Guid TeamId,
    Guid HostId,
    string Name,
    string Environment,
    string? Image,
    string? CommandJson,
    string? ComposeYaml,
    int? InternalPort,
    string? Hostname,
    bool Exposed,
    bool RequiresApproval,
    string Status,
    bool AllowDatabaseImages);

public sealed record DesiredState(
    string? Image,
    string? CommandJson,
    string? ComposeYaml,
    int? InternalPort,
    string? Hostname,
    bool Exposed);

public interface IWorkloadStore
{
    Task<WorkloadSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task SetStatusAsync(Guid id, string status, CancellationToken cancellationToken);

    Task ReplaceDesiredAsync(Guid id, DesiredState desired, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkloadSnapshot>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkloadSnapshot>>([]);
}
