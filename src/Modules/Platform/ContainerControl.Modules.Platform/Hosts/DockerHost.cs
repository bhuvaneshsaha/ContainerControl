namespace ContainerControl.Modules.Platform.Hosts;

public sealed class DockerHost
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string? EngineVersion { get; set; }

    public DateTimeOffset? LastPingAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed record DockerHostSnapshot(Guid Id, string Name, string Endpoint);

public interface IDockerHostLookup
{
    Task<DockerHostSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken);
}
