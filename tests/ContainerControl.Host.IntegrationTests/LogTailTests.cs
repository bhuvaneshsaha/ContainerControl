using System.Security.Claims;
using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Runtime.Http;
using ContainerControl.Modules.Runtime.Inspection;
using ContainerControl.SharedKernel.Authorization;
using ContainerControl.SharedKernel.CurrentUser;

namespace ContainerControl.Host.IntegrationTests;

public class LogTailTests
{
    [Fact]
    public void Live_log_hub_requires_the_logs_permission()
    {
        var allowed = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PermissionPolicy.ClaimType, PermissionCatalog.RuntimeLogsRead)],
            authenticationType: "test"));
        var denied = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PermissionPolicy.ClaimType, PermissionCatalog.RuntimeStatsRead)],
            authenticationType: "test"));

        Assert.True(LogHub.CanRead(allowed));
        Assert.False(LogHub.CanRead(denied));
        Assert.False(LogHub.CanRead(new ClaimsPrincipal()));
    }

    [Fact]
    public async Task Follow_writes_engine_log_lines_for_a_team_member()
    {
        var appId = Guid.NewGuid();
        var inspector = Inspector(member: true, appId, new FakeEngine(["hello", "again"]));
        var lines = new List<string>();

        await inspector.FollowAsync(appId, line =>
        {
            lines.Add(line);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(["web", "web hello", "web again"], lines);
    }

    [Fact]
    public async Task Follow_hides_an_application_the_caller_cannot_see()
    {
        var appId = Guid.NewGuid();
        var inspector = Inspector(member: false, appId, new FakeEngine(["secret"]));

        var error = await Assert.ThrowsAsync<LogStreamException>(() =>
            inspector.FollowAsync(appId, _ => Task.CompletedTask, CancellationToken.None));

        Assert.Equal("The application was not found.", error.Message);
    }

    private static RuntimeInspector Inspector(bool member, Guid appId, FakeEngine engine)
    {
        var app = new WorkloadSnapshot(appId, Guid.NewGuid(), Guid.NewGuid(), "welcome", "dev", null, null, null, null, null, false, false, "running");
        return new RuntimeInspector(
            new FakeApps(app),
            new FakeHosts(),
            engine,
            new FakeTeams(member),
            new FakeUser(Guid.NewGuid()));
    }

    private sealed class FakeApps(WorkloadSnapshot app) : IWorkloadStore
    {
        public Task<WorkloadSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<WorkloadSnapshot?>(id == app.Id ? app : null);

        public Task SetStatusAsync(Guid id, string status, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReplaceDesiredAsync(Guid id, DesiredState desired, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeHosts : IDockerHostLookup
    {
        public Task<DockerHostSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<DockerHostSnapshot?>(new DockerHostSnapshot(id, "local", "unix:///var/run/docker.sock"));
    }

    private sealed class FakeTeams(bool member) : ITeamDirectory
    {
        public Task<bool> IsMemberAsync(Guid userId, Guid teamId, CancellationToken cancellationToken) =>
            Task.FromResult(member);

        public Task<IReadOnlyList<Guid>> TeamIdsForAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class FakeUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => id;
    }

    private sealed class FakeEngine(IReadOnlyList<string> lines) : IDockerEngine
    {
        public Task FollowLogsAsync(DockerEndpoint endpoint, string containerId, int tail, IProgress<string> progress, CancellationToken cancellationToken)
        {
            foreach (var line in lines)
            {
                progress.Report(line);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EngineContainer>> ListByLabelAsync(DockerEndpoint endpoint, string label, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EngineContainer>>([new EngineContainer("id", "/web", true, null)]);

        public Task<EngineVersion> GetVersionAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListContainerIdsAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EnsureNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task PullImageAsync(DockerEndpoint endpoint, string image, ImagePullAuth? auth, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> CreateContainerAsync(DockerEndpoint endpoint, ContainerPlan plan, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ExtractArchiveAsync(DockerEndpoint endpoint, string containerId, string destinationPath, Stream archive, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task StartContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task StopContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> ExecAsync(DockerEndpoint endpoint, string containerId, IReadOnlyList<string> command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> ReadLogsAsync(DockerEndpoint endpoint, string containerId, int tail, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ContainerSample> ReadStatsAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> FindContainerIdByNameAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> ReadHealthStatusAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<HostCapacity> ReadCapacityAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
