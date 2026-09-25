using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Platform.Alerts;
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
public sealed class SlotDeployTests
{
    private readonly ApiFixture _fixture;

    public SlotDeployTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Slot_deploy_keeps_the_live_container_and_swap_moves_the_weighted_route()
    {
        var appId = Guid.NewGuid();
        var engine = new RecordingEngine(appId);
        await using var db = OpenDb();
        var service = Service(db, App(appId, exposed: true), engine, new RecordingAlerts());

        var deployed = await service.SlotDeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false);
        Assert.True(deployed.Succeeded, deployed.Error);
        Assert.DoesNotContain("live-1", engine.Removed);
        Assert.Contains(engine.Containers, container => container.Name.EndsWith("-app-blue", StringComparison.Ordinal));
        var parked = engine.Created.Last(plan => plan.Name.EndsWith("-route", StringComparison.Ordinal));
        Assert.Contains("weight=100;", parked.Environment[SlotRouteLabels.ProxyEnvironment], StringComparison.Ordinal);
        Assert.DoesNotContain("-blue:", parked.Environment[SlotRouteLabels.ProxyEnvironment], StringComparison.Ordinal);
        Assert.DoesNotContain(parked.Labels.Keys, key => key.Contains("weighted", StringComparison.Ordinal));
        Assert.DoesNotContain(engine.Created.Single(plan => plan.Name.EndsWith("-app-blue", StringComparison.Ordinal)).Labels.Keys, key => key.Contains(".rule", StringComparison.Ordinal));

        var canary = await service.CanaryAsync(appId, 25, CancellationToken.None);
        Assert.True(canary.Succeeded, canary.Error);
        var weighted = engine.Created.Last(plan => plan.Name.EndsWith("-route", StringComparison.Ordinal));
        var proxy = weighted.Environment[SlotRouteLabels.ProxyEnvironment];
        Assert.Contains("weight=75;", proxy, StringComparison.Ordinal);
        Assert.Contains("-blue:80 weight=25;", proxy, StringComparison.Ordinal);

        var swapped = await service.SwapSlotsAsync(appId, CancellationToken.None);
        Assert.True(swapped.Succeeded, swapped.Error);
        Assert.DoesNotContain("live-1", engine.Removed);
        var live = engine.Created.Last(plan => plan.Name.EndsWith("-route", StringComparison.Ordinal));
        var swappedProxy = live.Environment[SlotRouteLabels.ProxyEnvironment];
        Assert.Contains("-blue:80 weight=100;", swappedProxy, StringComparison.Ordinal);
        Assert.DoesNotContain("weight=75;", swappedProxy, StringComparison.Ordinal);

        await using var read = OpenDb();
        var row = await read.TrafficSlots.AsNoTracking().SingleAsync(item => item.ApplicationId == appId);
        Assert.Equal(TrafficPlan.Blue, row.LiveSlot);
        Assert.Equal(TrafficPlan.Classic, row.PreviousSlot);
        Assert.Null(row.CandidateSlot);
        Assert.Equal(0, row.CandidatePercent);
    }

    [Fact]
    public async Task A_failed_slot_deploy_leaves_the_live_container_and_alerts_without_a_secret()
    {
        var appId = Guid.NewGuid();
        var engine = new RecordingEngine(appId, failBlue: true);
        var alerts = new RecordingAlerts();
        await using var db = OpenDb();
        var outcome = await Service(db, App(appId, exposed: true), engine, alerts)
            .SlotDeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false);

        Assert.Equal(502, outcome.StatusCode);
        Assert.DoesNotContain("live-1", engine.Removed);
        Assert.DoesNotContain("failed", FixedWorkloads.Statuses);
        Assert.Equal(AlertKinds.DeployFailed, Assert.Single(alerts.Notices).Kind);
        Assert.DoesNotContain("super-secret", outcome.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", alerts.Notices[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unexposed_application_is_not_a_slot_deploy()
    {
        var appId = Guid.NewGuid();
        var engine = new RecordingEngine(appId);
        await using var db = OpenDb();
        var outcome = await Service(db, App(appId, exposed: false), engine, new RecordingAlerts())
            .SlotDeployAsync(appId, Guid.NewGuid(), approved: true, CancellationToken.None, requireMembership: false);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal("Blue/green needs an exposed service with a hostname.", outcome.Error);
        Assert.Empty(engine.Created);
        Assert.Empty(FixedWorkloads.Statuses);
    }

    [Fact]
    public async Task Canary_rejects_a_percent_outside_1_to_99()
    {
        var appId = Guid.NewGuid();
        await using var db = OpenDb();
        var outcome = await Service(db, App(appId, exposed: true), new RecordingEngine(appId), new RecordingAlerts())
            .CanaryAsync(appId, 100, CancellationToken.None);

        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal("Enter a canary percent from 1 to 99.", outcome.Error);
    }

    private DeliveryDbContext OpenDb() =>
        new(_fixture.Factory.Services.GetRequiredService<DbContextOptions<DeliveryDbContext>>());

    private static DeployService Service(DeliveryDbContext db, WorkloadSnapshot app, RecordingEngine engine, RecordingAlerts alerts)
    {
        FixedWorkloads.Statuses.Clear();
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 25, 18, 0, 0, TimeSpan.Zero));
        return new DeployService(
            db,
            new FixedWorkloads(app),
            new FixedHosts(),
            engine,
            new EmptySecrets(),
            new UnusedSecretStore(),
            new OpenEdge(),
            new NoRegistry(),
            new NoQuota(),
            new Members(),
            new FixedUser(Guid.NewGuid()),
            clock,
            new DeployLease(db, clock, NullLogger<DeployLease>.Instance),
            NullLogger<DeployService>.Instance,
            alerts);
    }

    private static WorkloadSnapshot App(Guid id, bool exposed) => new(
        id,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "billing",
        "dev",
        "busybox:1.36.1",
        null,
        null,
        exposed ? 80 : null,
        exposed ? "web.apps.example.com" : null,
        exposed,
        false,
        "running",
        false);

    private sealed class RecordingAlerts : IAlertPublisher
    {
        public List<AlertNotice> Notices { get; } = [];

        public Task PublishAsync(AlertNotice notice, CancellationToken cancellationToken)
        {
            Notices.Add(notice);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEngine : IDockerEngine
    {
        private readonly Guid _appId;
        private readonly bool _failBlue;
        private int _ids;

        public RecordingEngine(Guid appId, bool failBlue = false)
        {
            _appId = appId;
            _failBlue = failBlue;
            Containers.Add(new EngineContainer(
                "live-1",
                "/cc-live-app",
                true,
                "10.0.0.8",
                new Dictionary<string, string>
                {
                    [ManagedLabels.Application] = appId.ToString(),
                    [ManagedLabels.Service] = "app"
                }));
        }

        public List<EngineContainer> Containers { get; } = [];

        public List<ContainerPlan> Created { get; } = [];

        public List<string> Removed { get; } = [];

        public Task<IReadOnlyList<EngineContainer>> ListByLabelAsync(DockerEndpoint endpoint, string label, CancellationToken cancellationToken)
        {
            var expected = ManagedLabels.Application + "=" + _appId;
            IReadOnlyList<EngineContainer> matches = label == expected
                ? Containers.ToArray()
                : Array.Empty<EngineContainer>();
            return Task.FromResult(matches);
        }

        public Task<string> CreateContainerAsync(DockerEndpoint endpoint, ContainerPlan plan, CancellationToken cancellationToken)
        {
            if (_failBlue && plan.Name.EndsWith("-blue", StringComparison.Ordinal))
            {
                throw new DockerEngineException("The engine refused the image.");
            }

            Created.Add(plan);
            var id = "c" + Interlocked.Increment(ref _ids);
            Containers.Add(new EngineContainer(id, "/" + plan.Name, true, "10.0.0.9", plan.Labels));
            return Task.FromResult(id);
        }

        public Task RemoveContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken)
        {
            Removed.Add(containerId);
            Containers.RemoveAll(container => container.Id == containerId);
            return Task.CompletedTask;
        }

        public Task EnsureNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PullImageAsync(DockerEndpoint endpoint, string image, ImagePullAuth? auth, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ExtractArchiveAsync(DockerEndpoint endpoint, string containerId, string destinationPath, Stream archive, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<EngineVersion> GetVersionAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(new EngineVersion("test", "1.44"));

        public Task<IReadOnlyList<string>> ListContainerIdsAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

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
            Task.FromResult<string?>(null);

        public Task<HostCapacity> ReadCapacityAsync(DockerEndpoint endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(new HostCapacity(1, 1, 1));
    }

    private sealed class FixedWorkloads(WorkloadSnapshot app) : IWorkloadStore
    {
        public static List<string> Statuses { get; } = [];

        public Task<WorkloadSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<WorkloadSnapshot?>(id == app.Id ? app : null);

        public Task SetStatusAsync(Guid id, string status, CancellationToken cancellationToken)
        {
            Statuses.Add(status);
            return Task.CompletedTask;
        }

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
        public Task WriteAsync(SecretAddress address, string value, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(SecretAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> ReadAsync(SecretAddress address, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OpenEdge : IEdgeGateway
    {
        public string EdgeNetworkName => "edge";

        public Task<bool> HostnameAllowedAsync(string? hostname, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task EnsureEdgeAsync(string dockerEndpoint, CancellationToken cancellationToken) => Task.CompletedTask;

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
        public Task<bool> IsMemberAsync(Guid userId, Guid teamId, CancellationToken cancellationToken) => Task.FromResult(true);

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
