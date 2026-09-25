using System.Net;
using System.Text.Json;
using Docker.DotNet;
using Microsoft.AspNetCore.Http;
using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Platform.Alerts;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Platform.Quotas;
using ContainerControl.Modules.Registries.Connections;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Hostnames;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Delivery.Runs;

public sealed partial class DeployService
{
    private readonly DeliveryDbContext _db;
    private readonly IWorkloadStore _apps;
    private readonly IDockerHostLookup _hosts;
    private readonly IDockerEngine _engine;
    private readonly ISecretCatalog _secretCatalog;
    private readonly ISecretStore _secretStore;
    private readonly IEdgeGateway _edge;
    private readonly IRegistryLogin _registries;
    private readonly ITeamQuotaLookup _quotas;
    private readonly ITeamDirectory _teams;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly DeployLease _lease;
    private readonly ILogger<DeployService> _logger;
    private readonly IAlertPublisher _alerts;

    public DeployService(
        DeliveryDbContext db,
        IWorkloadStore apps,
        IDockerHostLookup hosts,
        IDockerEngine engine,
        ISecretCatalog secretCatalog,
        ISecretStore secretStore,
        IEdgeGateway edge,
        IRegistryLogin registries,
        ITeamQuotaLookup quotas,
        ITeamDirectory teams,
        ICurrentUser currentUser,
        IClock clock,
        DeployLease lease,
        ILogger<DeployService> logger,
        IAlertPublisher? alerts = null)
    {
        _db = db;
        _apps = apps;
        _hosts = hosts;
        _engine = engine;
        _secretCatalog = secretCatalog;
        _secretStore = secretStore;
        _edge = edge;
        _registries = registries;
        _quotas = quotas;
        _teams = teams;
        _currentUser = currentUser;
        _clock = clock;
        _lease = lease;
        _logger = logger;
        _alerts = alerts ?? SilentAlertPublisher.Instance;
    }

    public async Task<DeployOutcome> DeployAsync(
        Guid applicationId,
        Guid actorUserId,
        bool approved,
        CancellationToken cancellationToken,
        bool requireMembership = true)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null)
        {
            return DeployOutcome.NotFound();
        }

        if (requireMembership && (actorUserId == Guid.Empty || !await _teams.IsMemberAsync(actorUserId, app.TeamId, cancellationToken)))
        {
            return DeployOutcome.NotFound();
        }

        var ownerId = await _lease.TryAcquireAsync(app.Id, app.Environment, cancellationToken);
        if (ownerId is null)
        {
            return DeployOutcome.Fail(StatusCodes.Status409Conflict, DeployLease.ContendedMessage);
        }

        try
        {
            return await DeployHeldAsync(applicationId, approved, cancellationToken);
        }
        finally
        {
            await ReleaseHeldAsync(app.Id, app.Environment, ownerId.Value);
        }
    }

    public async Task<DeployOutcome> RollbackAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var previous = await _db.Deployments
            .AsNoTracking()
            .Where(item => item.ApplicationId == applicationId && item.Status == "succeeded")
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip(1)
            .FirstOrDefaultAsync(cancellationToken);
        if (previous is null)
        {
            return DeployOutcome.Fail(StatusCodes.Status409Conflict, "There is no previous successful deployment.");
        }

        try
        {
            await _apps.ReplaceDesiredAsync(applicationId, new DesiredState(
                previous.Image,
                previous.CommandJson,
                previous.ComposeYaml,
                previous.InternalPort,
                previous.Hostname,
                previous.Exposed), cancellationToken);
        }
        catch (ArgumentException exception) when (exception.Message == PublicHostname.InvalidMessage)
        {
            return DeployOutcome.Fail(StatusCodes.Status400BadRequest, PublicHostname.InvalidMessage);
        }

        return await DeployAsync(applicationId, _currentUser.UserId ?? Guid.Empty, approved: true, cancellationToken);
    }

    public async Task<DeployOutcome> ApproveAsync(
        Guid applicationId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var latest = await _db.Deployments
            .AsNoTracking()
            .Where(item => item.ApplicationId == applicationId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest?.Status == "pending-approval" && latest.Mode == TrafficPlan.SlotMode)
        {
            return await SlotDeployAsync(applicationId, actorUserId, approved: true, cancellationToken, requireMembership: false);
        }

        return await DeployAsync(applicationId, actorUserId, approved: true, cancellationToken, requireMembership: false);
    }

    public Task<DeployOutcome> ChangePowerAsync(Guid applicationId, string action, CancellationToken cancellationToken) =>
        WithMembership(applicationId, async app =>
        {
            var host = await _hosts.FindAsync(app.HostId, cancellationToken);
            if (host is null)
            {
                return DeployOutcome.Fail(StatusCodes.Status404NotFound, "The Docker host is gone.");
            }

            var endpoint = new DockerEndpoint(host.Endpoint);
            var containers = await _engine.ListByLabelAsync(endpoint, "cc.application=" + app.Id, cancellationToken);
            foreach (var container in containers)
            {
                if (action == "stop")
                {
                    await _engine.StopContainerAsync(endpoint, container.Id, cancellationToken);
                }
                else
                {
                    if (action == "restart" && container.Running)
                    {
                        await _engine.StopContainerAsync(endpoint, container.Id, cancellationToken);
                    }

                    await _engine.StartContainerAsync(endpoint, container.Id, cancellationToken);
                }
            }

            var status = action == "stop" ? "stopped" : "running";
            await _apps.SetStatusAsync(app.Id, status, cancellationToken);
            return DeployOutcome.Ok(status);
        }, cancellationToken);

    public async Task<IReadOnlyList<DeploymentRecord>> HistoryAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null || !await MemberAsync(applicationId, _currentUser.UserId.Value, cancellationToken))
        {
            return [];
        }

        return await _db.Deployments
            .AsNoTracking()
            .Where(item => item.ApplicationId == applicationId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeployOutcome> RemoveAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null || _currentUser.UserId is null || !await _teams.IsMemberAsync(_currentUser.UserId.Value, app.TeamId, cancellationToken))
        {
            return DeployOutcome.NotFound();
        }

        if (await _lease.IsContendedAsync(app.Id, app.Environment, cancellationToken))
        {
            return DeployOutcome.Fail(StatusCodes.Status409Conflict, DeployLease.ContendedMessage);
        }

        var host = await _hosts.FindAsync(app.HostId, cancellationToken);
        if (host is null)
        {
            _logger.LogWarning(
                "Application {ApplicationId} has no Docker host record. The application row will still be removed.",
                app.Id);
        }
        else
        {
            try
            {
                await RemoveRuntimeAsync(host.Endpoint, app.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    "Removing containers for {ApplicationId} failed. {ExceptionType}",
                    app.Id,
                    exception.GetType().Name);
                return DeployOutcome.Fail(
                    StatusCodes.Status502BadGateway,
                    "The Docker host could not remove the application's containers.");
            }
        }

        await _db.Deployments.Where(item => item.ApplicationId == app.Id).ExecuteDeleteAsync(cancellationToken);
        await _db.Leases.Where(item => item.ApplicationId == app.Id).ExecuteDeleteAsync(cancellationToken);
        await _db.TrafficSlots.Where(item => item.ApplicationId == app.Id).ExecuteDeleteAsync(cancellationToken);
        var deleted = await _apps.DeleteAsync(app.Id, cancellationToken);
        if (!deleted)
        {
            return DeployOutcome.NotFound();
        }

        _logger.LogInformation("Removed application {ApplicationId}.", app.Id);
        return DeployOutcome.Ok("deleted");
    }

    private async Task<DeployOutcome> DeployHeldAsync(Guid applicationId, bool approved, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(applicationId, approved, "replace", countSecondSlot: false, cancellationToken);
        if (gate.Outcome is not null)
        {
            return gate.Outcome;
        }

        try
        {
            await ApplyAsync(gate.App!, gate.Host!.Endpoint, gate.Plan!, gate.AnyExposed, gate.PublicHost, cancellationToken);
        }
        catch (SecretAssignmentException exception)
        {
            _logger.LogWarning(
                "Deployment rejected for {ApplicationId}. {Reason}",
                gate.App!.Id,
                exception.Message);
            await RecordAsync(gate.App!, "rejected", exception.Message, cancellationToken);
            await _apps.SetStatusAsync(gate.App!.Id, "rejected", cancellationToken);
            return DeployOutcome.Fail(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                "Deployment failed for {ApplicationId}. {ExceptionType}",
                gate.App!.Id,
                exception.GetType().Name);
            var message = DeployFailureText.Describe(exception);
            await RecordAsync(gate.App!, "failed", message, cancellationToken);
            await _apps.SetStatusAsync(gate.App!.Id, "failed", cancellationToken);
            await NotifyAsync(gate.App!, AlertKinds.DeployFailed, message, cancellationToken);
            return DeployOutcome.Fail(StatusCodes.Status502BadGateway, message);
        }

        await RecordAsync(gate.App!, "succeeded", null, cancellationToken);
        await _apps.SetStatusAsync(gate.App!.Id, "running", cancellationToken);
        return DeployOutcome.Ok("running");
    }

    private async Task<DeployGate> GateAsync(
        Guid applicationId,
        bool approved,
        string mode,
        bool countSecondSlot,
        CancellationToken cancellationToken,
        bool keepApplicationStatus = false)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null)
        {
            return DeployGate.Stop(DeployOutcome.NotFound());
        }

        if (app.RequiresApproval && !approved)
        {
            await RecordAsync(app, "pending-approval", null, cancellationToken, mode);
            await _apps.SetStatusAsync(app.Id, "pending-approval", cancellationToken);
            return DeployGate.Stop(DeployOutcome.Ok("pending-approval"));
        }

        var plan = string.IsNullOrWhiteSpace(app.ComposeYaml)
            ? ComposePolicy.FromImage(app.Image ?? string.Empty, ReadCommand(app.CommandJson), app.Exposed, app.InternalPort, app.AllowDatabaseImages)
            : ComposePolicy.Parse(app.ComposeYaml, app.AllowDatabaseImages);
        if (!plan.Accepted)
        {
            await RecordAsync(app, "rejected", string.Join(" ", plan.Errors), cancellationToken, mode);
            if (!keepApplicationStatus)
            {
                await _apps.SetStatusAsync(app.Id, "rejected", cancellationToken);
            }

            return DeployGate.Stop(DeployOutcome.Fail(StatusCodes.Status400BadRequest, string.Join(" ", plan.Errors)));
        }

        var publicHost = PublicHostname.Normalize(app.Hostname);
        var duplicateHostname = ComposePolicy.DuplicatePublicHostname(plan.Services, publicHost);
        if (duplicateHostname is not null)
        {
            await RecordAsync(app, "rejected", duplicateHostname, cancellationToken, mode);
            if (!keepApplicationStatus)
            {
                await _apps.SetStatusAsync(app.Id, "rejected", cancellationToken);
            }

            return DeployGate.Stop(DeployOutcome.Fail(StatusCodes.Status400BadRequest, duplicateHostname));
        }

        var resources = plan.Services.Select(service => service.Resources).ToList();
        if (countSecondSlot)
        {
            resources.AddRange(plan.Services.Select(service => service.Resources));
        }

        var quotaMessage = QuotaPolicy.Rejection(
            await _quotas.FindAsync(app.TeamId, cancellationToken),
            resources);
        if (quotaMessage is not null)
        {
            await RecordAsync(app, "rejected", quotaMessage, cancellationToken, mode);
            if (!keepApplicationStatus)
            {
                await _apps.SetStatusAsync(app.Id, "rejected", cancellationToken);
            }

            return DeployGate.Stop(DeployOutcome.Fail(StatusCodes.Status400BadRequest, quotaMessage));
        }

        var anyExposed = plan.Services.Any(service => service.Exposed) || (string.IsNullOrWhiteSpace(app.ComposeYaml) && app.Exposed);
        foreach (var service in plan.Services)
        {
            if (!service.Exposed)
            {
                continue;
            }

            var routeHost = service.Hostname ?? publicHost;
            if (PublicHostname.TraefikHostRule(routeHost) is null || !await _edge.HostnameAllowedAsync(routeHost, cancellationToken))
            {
                var message = routeHost is null
                    ? "Set a hostname under an allowed domain."
                    : "The hostname '" + routeHost + "' is not under an allowed domain.";
                await RecordAsync(app, "rejected", message, cancellationToken, mode);
                if (!keepApplicationStatus)
                {
                    await _apps.SetStatusAsync(app.Id, "rejected", cancellationToken);
                }

                return DeployGate.Stop(DeployOutcome.Fail(StatusCodes.Status400BadRequest, message));
            }
        }

        var host = await _hosts.FindAsync(app.HostId, cancellationToken);
        if (host is null)
        {
            return DeployGate.Stop(DeployOutcome.Fail(StatusCodes.Status404NotFound, "The Docker host is gone."));
        }

        return new DeployGate(null, app, plan, publicHost, anyExposed, host);
    }

    private async Task ApplyAsync(WorkloadSnapshot app, string endpointAddress, ComposePlan plan, bool anyExposed, string? publicHost, CancellationToken cancellationToken)
    {
        var catalog = await _secretCatalog.ListAsync(app.TeamId, app.Environment, cancellationToken);
        var assignments = catalog.Select(secret => (Name: secret.Name, Services: secret.ServiceNames)).ToArray();
        foreach (var service in plan.Services)
        {
            var failure = SecretInjection.AssignmentFailure(service.Name, service.Environment, service.Command, assignments);
            if (failure is not null)
            {
                throw new SecretAssignmentException(failure);
            }
        }

        var endpoint = new DockerEndpoint(endpointAddress);
        var network = AppNetwork(app.Id);
        var project = ComposeProjectName.Resolve(app.ComposeYaml, app.Name, app.Id);
        await _engine.EnsureNetworkAsync(endpoint, network, cancellationToken);
        if (anyExposed)
        {
            await _edge.EnsureEdgeAsync(endpointAddress, cancellationToken);
        }

        var serviceNames = plan.Services.Select(service => service.Name).ToHashSet(StringComparer.Ordinal);
        var targeted = catalog
            .Where(secret => secret.ServiceNames.Any(name => serviceNames.Contains(name)))
            .ToArray();
        var secretValues = new Dictionary<string, (string Value, string Mode)>(StringComparer.Ordinal);
        foreach (var secret in targeted)
        {
            var value = await _secretStore.ReadAsync(new SecretAddress(app.Environment, secret.Path), cancellationToken);
            if (value is null)
            {
                throw new DockerEngineException("A secret could not be read.");
            }

            secretValues[secret.Name] = (value, secret.InjectionMode);
        }

        var scoped = targeted
            .Select(secret => new ScopedSecret(secret.Name, secretValues[secret.Name].Value, secretValues[secret.Name].Mode, secret.ServiceNames))
            .ToArray();

        var existing = await _engine.ListByLabelAsync(endpoint, "cc.application=" + app.Id, cancellationToken);
        foreach (var container in existing)
        {
            await _engine.RemoveContainerAsync(endpoint, container.Id, cancellationToken);
        }

        await ClearSlotStateAsync(app.Id, endpoint, cancellationToken);

        var ordered = ComposePolicy.StartOrder(plan.Services);
        foreach (var service in ordered)
        {
            var image = service.Image;
            var auth = await _registries.ForImageAsync(image, cancellationToken);
            await _engine.PullImageAsync(endpoint, image, auth, cancellationToken);
            var injected = SecretInjection.Apply(service.Environment, service.Command, SecretInjection.ForService(service.Name, scoped));
            var labels = new Dictionary<string, string>
            {
                ["cc.managed"] = "true",
                ["cc.application"] = app.Id.ToString(),
                ["cc.service"] = service.Name
            };
            ComposeProjectName.Stamp(labels, project, service.Name);
            var extra = new List<string>();
            var routeHost = ComposePolicy.RouteHostname(service, publicHost);
            if (routeHost is not null && service.Port is int port)
            {
                foreach (var (key, value) in _edge.LabelsFor(RouterName(app.Id, service.Name), routeHost, port))
                {
                    labels[key] = value;
                }

                extra.Add(_edge.EdgeNetworkName);
            }

            var id = await _engine.CreateContainerAsync(endpoint, new ContainerPlan(
                ContainerName(app.Id, service.Name),
                image,
                injected.Command,
                labels,
                injected.Environment,
                network,
                service.Name,
                extra,
                [],
                new Dictionary<string, string>(),
                "unless-stopped",
                service.Healthcheck,
                service.Resources is null ? 0 : service.Resources.CpuMillicores * 1_000_000L,
                service.Resources?.MemoryBytes ?? 0), cancellationToken);
            try
            {
                if (injected.Files.Count > 0)
                {
                    var archive = SecretArchive.Create(injected.Files);
                    await using var stream = new MemoryStream(archive, writable: false);
                    await _engine.ExtractArchiveAsync(endpoint, id, "/", stream, cancellationToken);
                }

                await _engine.StartContainerAsync(endpoint, id, cancellationToken);
                if (service.Healthcheck is not null)
                {
                    await HealthcheckGate.WaitAsync(
                        token => _engine.ReadHealthStatusAsync(endpoint, id, token),
                        HealthcheckGate.Budget(service.Healthcheck),
                        TimeSpan.FromSeconds(1),
                        cancellationToken);
                }
            }
            catch (Exception)
            {
                try
                {
                    await _engine.RemoveContainerAsync(endpoint, id, cancellationToken);
                }
                catch (Exception cleanupError) when (cleanupError is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        "Removing container {ContainerId} after a failed start did not succeed. {ExceptionType}",
                        id,
                        cleanupError.GetType().Name);
                }

                throw;
            }
        }
    }

    private async Task RecordAsync(WorkloadSnapshot app, string status, string? error, CancellationToken cancellationToken, string mode = "replace")
    {
        _db.Deployments.Add(new DeploymentRecord
        {
            Id = Guid.NewGuid(),
            ApplicationId = app.Id,
            Image = app.Image,
            CommandJson = app.CommandJson,
            ComposeYaml = app.ComposeYaml,
            InternalPort = app.InternalPort,
            Hostname = app.Hostname,
            Exposed = app.Exposed,
            Status = status,
            Mode = mode,
            Error = error,
            CreatedAtUtc = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyAsync(WorkloadSnapshot app, string kind, string message, CancellationToken cancellationToken)
    {
        try
        {
            await _alerts.PublishAsync(new AlertNotice(kind, app.Id, app.Name, message), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Alert {AlertKind} was not sent for {ApplicationId}. {ExceptionType}",
                kind,
                app.Id,
                exception.GetType().Name);
        }
    }

    private async Task ReleaseHeldAsync(Guid applicationId, string environment, Guid ownerId)
    {
        try
        {
            _db.ChangeTracker.Clear();
            await _lease.ReleaseAsync(applicationId, environment, ownerId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Releasing the deploy lease failed for {ApplicationId} in {Environment}. {ExceptionType}",
                applicationId,
                environment,
                exception.GetType().Name);
        }
    }

    private async Task<bool> MemberAsync(Guid applicationId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null || actorUserId == Guid.Empty)
        {
            return false;
        }

        return await _teams.IsMemberAsync(actorUserId, app.TeamId, cancellationToken);
    }

    private async Task<DeployOutcome> WithMembership(
        Guid applicationId,
        Func<WorkloadSnapshot, Task<DeployOutcome>> action,
        CancellationToken cancellationToken)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null || _currentUser.UserId is null || !await _teams.IsMemberAsync(_currentUser.UserId.Value, app.TeamId, cancellationToken))
        {
            return DeployOutcome.NotFound();
        }

        return await action(app);
    }

    private static IReadOnlyList<string>? ReadCommand(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<string>>(json);
    }

    private async Task RemoveRuntimeAsync(string endpointAddress, Guid applicationId, CancellationToken cancellationToken)
    {
        var endpoint = new DockerEndpoint(endpointAddress);
        var containers = await _engine.ListByLabelAsync(endpoint, "cc.application=" + applicationId, cancellationToken);
        foreach (var container in containers)
        {
            try
            {
                await _engine.RemoveContainerAsync(endpoint, container.Id, cancellationToken);
            }
            catch (DockerApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        await TryRemoveNetworkAsync(endpoint, AppNetwork(applicationId), applicationId);
        await TryRemoveNetworkAsync(endpoint, TrafficPlan.Network(applicationId, TrafficPlan.Blue), applicationId);
        await TryRemoveNetworkAsync(endpoint, TrafficPlan.Network(applicationId, TrafficPlan.Green), applicationId);
    }

    private async Task ClearSlotStateAsync(Guid applicationId, DockerEndpoint endpoint, CancellationToken cancellationToken)
    {
        await _db.TrafficSlots.Where(item => item.ApplicationId == applicationId).ExecuteDeleteAsync(cancellationToken);
        await TryRemoveNetworkAsync(endpoint, TrafficPlan.Network(applicationId, TrafficPlan.Blue), applicationId);
        await TryRemoveNetworkAsync(endpoint, TrafficPlan.Network(applicationId, TrafficPlan.Green), applicationId);
    }

    private async Task TryRemoveNetworkAsync(DockerEndpoint endpoint, string name, Guid applicationId)
    {
        try
        {
            await _engine.RemoveNetworkAsync(endpoint, name, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Application network {Network} for {ApplicationId} was not removed. {ExceptionType}",
                name,
                applicationId,
                exception.GetType().Name);
        }
    }

    private static string AppNetwork(Guid appId) => "cc-app-" + appId.ToString("N");

    private static string ContainerName(Guid appId, string service) =>
        "cc" + appId.ToString("N")[..12] + "-" + service;

    private static string RouterName(Guid appId, string service) =>
        "cc" + appId.ToString("N")[..12] + service.Replace("-", "");
}

internal sealed record DeployGate(
    DeployOutcome? Outcome,
    WorkloadSnapshot? App,
    ComposePlan? Plan,
    string? PublicHost,
    bool AnyExposed,
    DockerHostSnapshot? Host)
{
    public static DeployGate Stop(DeployOutcome outcome) => new(outcome, null, null, null, false, null);
}

public sealed record DeployOutcome(bool Succeeded, int StatusCode, string Status, string? Error)
{
    public static DeployOutcome Ok(string status) => new(true, StatusCodes.Status200OK, status, null);

    public static DeployOutcome NotFound() => new(false, StatusCodes.Status404NotFound, "not-found", "The application was not found.");

    public static DeployOutcome Fail(int statusCode, string error) => new(false, statusCode, "failed", error);
}
