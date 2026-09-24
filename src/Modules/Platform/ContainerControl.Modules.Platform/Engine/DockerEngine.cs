namespace ContainerControl.Modules.Platform.Engine;

public sealed record DockerEndpoint(string Address);

public sealed record EngineVersion(string Version, string ApiVersion);

public sealed record EngineContainer(string Id, string Name, bool Running, string? IpAddress);

public sealed record ContainerHealthcheck(
    IReadOnlyList<string> Test,
    TimeSpan Interval,
    TimeSpan Timeout,
    TimeSpan StartPeriod,
    int Retries);

public sealed record ContainerPlan(
    string Name,
    string Image,
    IReadOnlyList<string>? Command,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyDictionary<string, string> Environment,
    string Network,
    string NetworkAlias,
    IReadOnlyList<string> ExtraNetworks,
    IReadOnlyList<string> Binds,
    IReadOnlyDictionary<string, string> PublishedPorts,
    string RestartPolicy,
    ContainerHealthcheck? Healthcheck = null);

public interface IDockerEngine
{
    Task<EngineVersion> GetVersionAsync(DockerEndpoint endpoint, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListContainerIdsAsync(DockerEndpoint endpoint, CancellationToken cancellationToken);

    Task EnsureNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken);

    Task RemoveNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken);

    Task PullImageAsync(DockerEndpoint endpoint, string image, CancellationToken cancellationToken);

    Task<string> CreateContainerAsync(DockerEndpoint endpoint, ContainerPlan plan, CancellationToken cancellationToken);

    Task ExtractArchiveAsync(DockerEndpoint endpoint, string containerId, string destinationPath, Stream archive, CancellationToken cancellationToken);

    Task StartContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken);

    Task StopContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken);

    Task RemoveContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<EngineContainer>> ListByLabelAsync(
        DockerEndpoint endpoint,
        string label,
        CancellationToken cancellationToken);

    Task<string> ExecAsync(
        DockerEndpoint endpoint,
        string containerId,
        IReadOnlyList<string> command,
        CancellationToken cancellationToken);

    Task<string> ReadLogsAsync(
        DockerEndpoint endpoint,
        string containerId,
        int tail,
        CancellationToken cancellationToken);

    Task FollowLogsAsync(
        DockerEndpoint endpoint,
        string containerId,
        int tail,
        IProgress<string> progress,
        CancellationToken cancellationToken);

    Task<ContainerSample> ReadStatsAsync(
        DockerEndpoint endpoint,
        string containerId,
        CancellationToken cancellationToken);

    Task<string?> FindContainerIdByNameAsync(
        DockerEndpoint endpoint,
        string name,
        CancellationToken cancellationToken);

    Task<string?> ReadHealthStatusAsync(
        DockerEndpoint endpoint,
        string containerId,
        CancellationToken cancellationToken);
}

public sealed record ContainerSample(double CpuPercent, long MemoryBytes);
