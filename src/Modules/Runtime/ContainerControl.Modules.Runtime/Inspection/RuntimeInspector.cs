using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.SharedKernel.CurrentUser;

namespace ContainerControl.Modules.Runtime.Inspection;

public sealed record ServiceStats(string Service, double CpuPercent, long MemoryBytes);

public sealed class RuntimeInspector
{
    private readonly IWorkloadStore _apps;
    private readonly IDockerHostLookup _hosts;
    private readonly IDockerEngine _engine;
    private readonly ITeamDirectory _teams;
    private readonly ICurrentUser _currentUser;

    public RuntimeInspector(
        IWorkloadStore apps,
        IDockerHostLookup hosts,
        IDockerEngine engine,
        ITeamDirectory teams,
        ICurrentUser currentUser)
    {
        _apps = apps;
        _hosts = hosts;
        _engine = engine;
        _teams = teams;
        _currentUser = currentUser;
    }

    public async Task<string?> LogsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(applicationId, cancellationToken);
        if (located is null)
        {
            return null;
        }

        var (endpoint, containers) = located.Value;
        var parts = new List<string>();
        foreach (var container in containers)
        {
            var text = await _engine.ReadLogsAsync(endpoint, container.Id, 80, cancellationToken);
            parts.Add(container.Name.TrimStart('/') + "\n" + text);
        }

        return string.Join("\n", parts);
    }

    public async Task<IReadOnlyList<ServiceStats>?> StatsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(applicationId, cancellationToken);
        if (located is null)
        {
            return null;
        }

        var (endpoint, containers) = located.Value;
        var stats = new List<ServiceStats>();
        foreach (var container in containers.Where(item => item.Running))
        {
            var sample = await _engine.ReadStatsAsync(endpoint, container.Id, cancellationToken);
            stats.Add(new ServiceStats(container.Name.TrimStart('/'), sample.CpuPercent, sample.MemoryBytes));
        }

        return stats;
    }

    private async Task<(DockerEndpoint Endpoint, IReadOnlyList<EngineContainer> Containers)?> LocateAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null || _currentUser.UserId is null || !await _teams.IsMemberAsync(_currentUser.UserId.Value, app.TeamId, cancellationToken))
        {
            return null;
        }

        var host = await _hosts.FindAsync(app.HostId, cancellationToken);
        if (host is null)
        {
            return null;
        }

        var endpoint = new DockerEndpoint(host.Endpoint);
        var containers = await _engine.ListByLabelAsync(endpoint, "cc.application=" + app.Id, cancellationToken);
        return (endpoint, containers);
    }
}
