using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Platform.Quotas;
using ContainerControl.Modules.Registries.Connections;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContainerControl.Host.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DeployLeaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 3, 0, 0, TimeSpan.Zero);

    private readonly ApiFixture _fixture;

    public DeployLeaseTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 20000)]
    public async Task Concurrent_deploys_of_different_applications_both_run_and_release()
    {
        var gate = new ArrivalGate(2);
        var firstApp = Guid.NewGuid();
        var secondApp = Guid.NewGuid();
        await using var firstDb = OpenDb();
        await using var secondDb = OpenDb();
        var first = Service(firstDb, App(firstApp, "dev"), new ScriptedEngine(gate.ArriveAsync));
        var second = Service(secondDb, App(secondApp, "dev"), new ScriptedEngine(gate.ArriveAsync));

        var results = await Task.WhenAll(
            first.DeployAsync(firstApp, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false),
            second.DeployAsync(secondApp, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false));

        Assert.True(results[0].Succeeded, results[0].Error);
        Assert.True(results[1].Succeeded, results[1].Error);
        Assert.Null((await ReadLeaseAsync(firstApp, "dev"))!.OwnerId);
        Assert.Null((await ReadLeaseAsync(secondApp, "dev"))!.OwnerId);
    }

    [Fact(Timeout = 20000)]
    public async Task Concurrent_deploys_of_the_same_application_in_different_environments_both_run()
    {
        var gate = new ArrivalGate(2);
        var appId = Guid.NewGuid();
        await using var devDb = OpenDb();
        await using var stagingDb = OpenDb();
        var dev = Service(devDb, App(appId, "dev"), new ScriptedEngine(gate.ArriveAsync));
        var staging = Service(stagingDb, App(appId, "staging"), new ScriptedEngine(gate.ArriveAsync));

        var results = await Task.WhenAll(
            dev.DeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false),
            staging.DeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false));

        Assert.True(results[0].Succeeded, results[0].Error);
        Assert.True(results[1].Succeeded, results[1].Error);
        Assert.Null((await ReadLeaseAsync(appId, "dev"))!.OwnerId);
        Assert.Null((await ReadLeaseAsync(appId, "staging"))!.OwnerId);
    }

    [Fact(Timeout = 20000)]
    public async Task A_second_deploy_of_the_same_application_and_environment_is_rejected_until_release()
    {
        var appId = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var idle = new ScriptedEngine();
        await using var heldDb = OpenDb();
        await using var otherDb = OpenDb();
        var held = Service(heldDb, App(appId, "prod"), new ScriptedEngine(async cancellationToken =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }));
        var other = Service(otherDb, App(appId, "prod"), idle);

        var first = held.DeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
        var blocked = await other.DeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false);

        Assert.False(blocked.Succeeded);
        Assert.Equal(409, blocked.StatusCode);
        Assert.Equal(DeployLease.ContendedMessage, blocked.Error);
        Assert.Equal(0, idle.Pulls);
        var row = await ReadLeaseAsync(appId, "prod");
        Assert.NotNull(row!.OwnerId);
        Assert.Equal(Now.Add(DeployLease.HoldFor), row.ExpiresAtUtc);

        release.TrySetResult();
        var done = await first;
        Assert.True(done.Succeeded, done.Error);
        Assert.Null((await ReadLeaseAsync(appId, "prod"))!.OwnerId);
    }

    [Fact]
    public async Task A_failed_deploy_releases_the_lease()
    {
        var appId = Guid.NewGuid();
        await using var db = OpenDb();
        var outcome = await Service(db, App(appId, "dev"), new ScriptedEngine(_ => throw new DockerEngineException("The engine refused the image.")))
            .DeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false);

        Assert.Equal(502, outcome.StatusCode);
        Assert.Null((await ReadLeaseAsync(appId, "dev"))!.OwnerId);
        await using var history = OpenDb();
        var deployment = await history.Deployments.AsNoTracking().SingleAsync(item => item.ApplicationId == appId);
        Assert.Equal("failed", deployment.Status);
    }

    [Fact(Timeout = 20000)]
    public async Task Cancelling_a_deploy_releases_the_lease()
    {
        var appId = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var db = OpenDb();
        var service = Service(db, App(appId, "dev"), new ScriptedEngine(async cancellationToken =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }));
        using var cts = new CancellationTokenSource();
        var pending = service.DeployAsync(appId, Guid.NewGuid(), approved: true, cts.Token, requireMembership: false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        Assert.Null((await ReadLeaseAsync(appId, "dev"))!.OwnerId);
    }

    [Fact]
    public async Task An_expired_lease_does_not_block_the_next_deploy()
    {
        var appId = Guid.NewGuid();
        await using (var seed = OpenDb())
        {
            seed.Leases.Add(new WorkerLease
            {
                ApplicationId = appId,
                Environment = "dev",
                OwnerId = Guid.NewGuid(),
                ExpiresAtUtc = Now.AddMinutes(-1)
            });
            await seed.SaveChangesAsync();
        }

        await using var db = OpenDb();
        var outcome = await Service(db, App(appId, "dev"), new ScriptedEngine())
            .DeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false);

        Assert.True(outcome.Succeeded, outcome.Error);
        Assert.Null((await ReadLeaseAsync(appId, "dev"))!.OwnerId);
    }

    private DeliveryDbContext OpenDb() =>
        new(_fixture.Factory.Services.GetRequiredService<DbContextOptions<DeliveryDbContext>>());

    private async Task<WorkerLease?> ReadLeaseAsync(Guid applicationId, string environment)
    {
        await using var db = OpenDb();
        return await db.Leases.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ApplicationId == applicationId && item.Environment == environment);
    }

    private static DeployService Service(DeliveryDbContext db, WorkloadSnapshot app, ScriptedEngine engine)
    {
        var clock = new FixedClock(Now);
        return new DeployService(
            db,
            new FixedWorkloads(app),
            new FixedHosts(),
            engine,
            new EmptySecrets(),
            new UnusedSecretStore(),
            new UnusedEdge(),
            new NoRegistry(),
            new NoQuota(),
            new Members(),
            new FixedUser(Guid.NewGuid()),
            clock,
            new DeployLease(db, clock, NullLogger<DeployLease>.Instance),
            NullLogger<DeployService>.Instance);
    }

    private static WorkloadSnapshot App(Guid id, string environment) => new(
        id,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "billing",
        environment,
        "busybox:1.36.1",
        null,
        null,
        null,
        null,
        false,
        false,
        "registered",
        false);

    private sealed class ArrivalGate
    {
        private readonly int _expected;
        private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public ArrivalGate(int expected) => _expected = expected;

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrived) >= _expected)
            {
                _all.TrySetResult();
            }

            await _all.Task.WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
        }
    }

    private sealed class ScriptedEngine : IDockerEngine
    {
        private readonly Func<CancellationToken, Task>? _pull;
        private int _pulls;

        public ScriptedEngine(Func<CancellationToken, Task>? pull = null) => _pull = pull;

        public int Pulls => Volatile.Read(ref _pulls);

        public async Task PullImageAsync(DockerEndpoint endpoint, string image, ImagePullAuth? auth, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _pulls);
            if (_pull is not null)
            {
                await _pull(cancellationToken);
            }
        }

        public Task<EngineVersion> GetVersionAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(new EngineVersion("test", "1.44"));

        public Task<IReadOnlyList<string>> ListContainerIdsAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task EnsureNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<string> CreateContainerAsync(DockerEndpoint endpoint, ContainerPlan plan, CancellationToken cancellationToken) =>
            Task.FromResult("container");

        public Task ExtractArchiveAsync(DockerEndpoint endpoint, string containerId, string destinationPath, Stream archive, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StartContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StopContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<EngineContainer>> ListByLabelAsync(DockerEndpoint endpoint, string label, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EngineContainer>>([]);

        public Task<string> ExecAsync(DockerEndpoint endpoint, string containerId, IReadOnlyList<string> command, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> ReadLogsAsync(DockerEndpoint endpoint, string containerId, int tail, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task FollowLogsAsync(DockerEndpoint endpoint, string containerId, int tail, IProgress<string> progress, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ContainerSample> ReadStatsAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ContainerSample(0, 0));

        public Task<string?> FindContainerIdByNameAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task<string?> ReadHealthStatusAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("healthy");

        public Task<HostCapacity> ReadCapacityAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(new HostCapacity(1, 1, 1));
    }

    private sealed class FixedWorkloads(WorkloadSnapshot app) : IWorkloadStore
    {
        public Task<WorkloadSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<WorkloadSnapshot?>(id == app.Id ? app : null);

        public Task SetStatusAsync(Guid id, string status, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReplaceDesiredAsync(Guid id, DesiredState desired, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FixedHosts : IDockerHostLookup
    {
        public Task<DockerHostSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<DockerHostSnapshot?>(new DockerHostSnapshot(id, "host", "tcp://engine:2376"));
    }

    private sealed class EmptySecrets : ISecretCatalog
    {
        public Task<IReadOnlyList<SecretSnapshot>> ListAsync(Guid teamId, string environment, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SecretSnapshot>>([]);
    }

    private sealed class UnusedSecretStore : ISecretStore
    {
        public Task WriteAsync(SecretAddress address, string value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(SecretAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> ReadAsync(SecretAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedEdge : IEdgeGateway
    {
        public string EdgeNetworkName => "edge";

        public Task<bool> HostnameAllowedAsync(string? hostname, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task EnsureEdgeAsync(string dockerEndpoint, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public IReadOnlyDictionary<string, string> LabelsFor(string routerName, string hostname, int port) =>
            throw new NotSupportedException();
    }

    private sealed class NoRegistry : IRegistryLogin
    {
        public Task<ImagePullAuth?> ForImageAsync(string image, CancellationToken cancellationToken) =>
            Task.FromResult<ImagePullAuth?>(null);
    }

    private sealed class NoQuota : ITeamQuotaLookup
    {
        public Task<ServiceResources?> FindAsync(Guid teamId, CancellationToken cancellationToken) =>
            Task.FromResult<ServiceResources?>(null);
    }

    private sealed class Members : ITeamDirectory
    {
        public Task<bool> IsMemberAsync(Guid userId, Guid teamId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<Guid>> TeamIdsForAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => id;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
