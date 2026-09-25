using System.Net;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Platform.Alerts;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.SharedKernel.Hostnames;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Delivery.Runs;

public sealed partial class DeployService
{
    public async Task<DeployOutcome> SlotDeployAsync(
        Guid applicationId,
        Guid actorUserId,
        bool approved,
        CancellationToken cancellationToken,
        bool requireMembership = true)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null || requireMembership && (actorUserId == Guid.Empty || !await _teams.IsMemberAsync(actorUserId, app.TeamId, cancellationToken)))
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
            return await SlotDeployHeldAsync(applicationId, approved, cancellationToken);
        }
        finally
        {
            await ReleaseHeldAsync(app.Id, app.Environment, ownerId.Value);
        }
    }

    public async Task<DeployOutcome> SwapSlotsAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await ShiftAsync(applicationId, candidatePercent: 100, swap: true, cancellationToken);

    public async Task<DeployOutcome> CanaryAsync(Guid applicationId, int percent, CancellationToken cancellationToken)
    {
        if (percent is < 1 or > 99)
        {
            return DeployOutcome.Fail(StatusCodes.Status400BadRequest, "Enter a canary percent from 1 to 99.");
        }

        return await ShiftAsync(applicationId, percent, swap: false, cancellationToken);
    }

    public async Task<DeployOutcome> RevertSlotsAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await WithMembership(applicationId, async app =>
        {
            var ownerId = await _lease.TryAcquireAsync(app.Id, app.Environment, cancellationToken);
            if (ownerId is null)
            {
                return DeployOutcome.Fail(StatusCodes.Status409Conflict, DeployLease.ContendedMessage);
            }

            try
            {
                var row = await _db.TrafficSlots.SingleOrDefaultAsync(item => item.ApplicationId == app.Id, cancellationToken);
                if (row?.PreviousSlot is null)
                {
                    return DeployOutcome.Fail(StatusCodes.Status409Conflict, "There is no previous release to restore traffic to.");
                }

                var gate = await GateAsync(app.Id, approved: true, TrafficPlan.SlotMode, countSecondSlot: false, cancellationToken, keepApplicationStatus: true);
                if (gate.Outcome is not null)
                {
                    return gate.Outcome;
                }

                if (!gate.AnyExposed)
                {
                    return DeployOutcome.Fail(StatusCodes.Status400BadRequest, ExposedRequired);
                }

                var current = row.LiveSlot;
                row.LiveSlot = row.PreviousSlot;
                row.PreviousSlot = current;
                row.CandidateSlot = null;
                row.CandidatePercent = 0;
                row.UpdatedAtUtc = _clock.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                await RefreshDirectorsAsync(gate.App!, gate.Host!.Endpoint, gate.Plan!, gate.PublicHost, row, cancellationToken);
                await RecordAsync(gate.App!, "succeeded", null, cancellationToken, "revert");
                return DeployOutcome.Ok("running");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var message = DeployFailureText.Describe(exception);
                _logger.LogError("Slot revert failed for {ApplicationId}. {ExceptionType}", app.Id, exception.GetType().Name);
                await NotifyAsync(app, AlertKinds.DeployFailed, message, cancellationToken);
                return DeployOutcome.Fail(StatusCodes.Status502BadGateway, message);
            }
            finally
            {
                await ReleaseHeldAsync(app.Id, app.Environment, ownerId.Value);
            }
        }, cancellationToken);

    public async Task<TrafficSlotView?> ReadSlotAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null || !await MemberAsync(applicationId, _currentUser.UserId.Value, cancellationToken))
        {
            return null;
        }

        var row = await _db.TrafficSlots.AsNoTracking().SingleOrDefaultAsync(item => item.ApplicationId == applicationId, cancellationToken);
        return row is null
            ? new TrafficSlotView(TrafficPlan.Classic, null, 0, null)
            : new TrafficSlotView(row.LiveSlot, row.CandidateSlot, row.CandidatePercent, row.PreviousSlot);
    }

    private async Task<DeployOutcome> SlotDeployHeldAsync(Guid applicationId, bool approved, CancellationToken cancellationToken)
    {
        var preview = await _apps.FindAsync(applicationId, cancellationToken);
        if (preview is null)
        {
            return DeployOutcome.NotFound();
        }

        bool hasLive;
        string liveSlot;
        try
        {
            var hostPreview = await _hosts.FindAsync(preview.HostId, cancellationToken);
            var existing = hostPreview is null
                ? Array.Empty<EngineContainer>()
                : await _engine.ListByLabelAsync(new DockerEndpoint(hostPreview.Endpoint), ManagedLabels.Application + "=" + preview.Id, cancellationToken);
            var current = await _db.TrafficSlots.AsNoTracking().SingleOrDefaultAsync(item => item.ApplicationId == preview.Id, cancellationToken);
            liveSlot = current?.LiveSlot ?? TrafficPlan.Classic;
            hasLive = existing.Any(container => IsBackend(container) && MatchesSlot(container, liveSlot));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = DeployFailureText.Describe(exception);
            await RecordAsync(preview, "failed", message, cancellationToken, TrafficPlan.SlotMode);
            await NotifyAsync(preview, AlertKinds.DeployFailed, message, cancellationToken);
            return DeployOutcome.Fail(StatusCodes.Status502BadGateway, message);
        }
        var gate = await GateAsync(applicationId, approved, TrafficPlan.SlotMode, countSecondSlot: hasLive, cancellationToken, keepApplicationStatus: hasLive);
        if (gate.Outcome is not null)
        {
            return gate.Outcome;
        }

        if (!gate.AnyExposed)
        {
            await RecordAsync(gate.App!, "rejected", ExposedRequired, cancellationToken, TrafficPlan.SlotMode);
            if (!hasLive)
            {
                await _apps.SetStatusAsync(gate.App!.Id, "rejected", cancellationToken);
            }

            return DeployOutcome.Fail(StatusCodes.Status400BadRequest, ExposedRequired);
        }

        var candidate = TrafficPlan.CandidateFor(hasLive ? liveSlot : null);
        try
        {
            await ApplySlotAsync(gate.App!, gate.Host!.Endpoint, gate.Plan!, gate.PublicHost, candidate, cancellationToken);
            var row = await _db.TrafficSlots.SingleOrDefaultAsync(item => item.ApplicationId == gate.App!.Id, cancellationToken);
            if (row is null)
            {
                row = new TrafficSlot { ApplicationId = gate.App!.Id };
                _db.TrafficSlots.Add(row);
            }

            if (!hasLive)
            {
                row.LiveSlot = candidate;
                row.CandidateSlot = null;
                row.CandidatePercent = 0;
                row.PreviousSlot = null;
            }
            else
            {
                row.LiveSlot = liveSlot;
                row.CandidateSlot = candidate;
                row.CandidatePercent = 0;
            }

            row.UpdatedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await RefreshDirectorsAsync(gate.App!, gate.Host!.Endpoint, gate.Plan!, gate.PublicHost, row, cancellationToken);
        }
        catch (SecretAssignmentException exception)
        {
            await RecordAsync(gate.App!, "rejected", exception.Message, cancellationToken, TrafficPlan.SlotMode);
            if (!hasLive)
            {
                await _apps.SetStatusAsync(gate.App!.Id, "rejected", cancellationToken);
            }

            return DeployOutcome.Fail(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RemoveSlotContainersAsync(new DockerEndpoint(gate.Host!.Endpoint), gate.App!.Id, candidate, cancellationToken);
            var message = DeployFailureText.Describe(exception);
            _logger.LogError("Slot deploy failed for {ApplicationId}. {ExceptionType}", gate.App!.Id, exception.GetType().Name);
            await RecordAsync(gate.App!, "failed", message, cancellationToken, TrafficPlan.SlotMode);
            if (!hasLive)
            {
                await _apps.SetStatusAsync(gate.App!.Id, "failed", cancellationToken);
            }

            await NotifyAsync(gate.App!, AlertKinds.DeployFailed, message, cancellationToken);
            return DeployOutcome.Fail(StatusCodes.Status502BadGateway, message);
        }

        await RecordAsync(gate.App!, "succeeded", null, cancellationToken, TrafficPlan.SlotMode);
        await _apps.SetStatusAsync(gate.App!.Id, "running", cancellationToken);
        return DeployOutcome.Ok("running");
    }

    private async Task<DeployOutcome> ShiftAsync(Guid applicationId, int candidatePercent, bool swap, CancellationToken cancellationToken) =>
        await WithMembership(applicationId, async app =>
        {
            var ownerId = await _lease.TryAcquireAsync(app.Id, app.Environment, cancellationToken);
            if (ownerId is null)
            {
                return DeployOutcome.Fail(StatusCodes.Status409Conflict, DeployLease.ContendedMessage);
            }

            try
            {
                var row = await _db.TrafficSlots.SingleOrDefaultAsync(item => item.ApplicationId == app.Id, cancellationToken);
                if (row?.CandidateSlot is null)
                {
                    return DeployOutcome.Fail(StatusCodes.Status409Conflict, "Deploy a release beside the live one before shifting traffic.");
                }

                var gate = await GateAsync(app.Id, approved: true, swap ? "swap" : "canary", countSecondSlot: false, cancellationToken, keepApplicationStatus: true);
                if (gate.Outcome is not null)
                {
                    return gate.Outcome;
                }

                if (!gate.AnyExposed)
                {
                    return DeployOutcome.Fail(StatusCodes.Status400BadRequest, ExposedRequired);
                }

                if (swap)
                {
                    row.PreviousSlot = row.LiveSlot;
                    row.LiveSlot = row.CandidateSlot;
                    row.CandidateSlot = null;
                    row.CandidatePercent = 0;
                }
                else
                {
                    row.CandidatePercent = candidatePercent;
                }

                row.UpdatedAtUtc = _clock.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                await RefreshDirectorsAsync(gate.App!, gate.Host!.Endpoint, gate.Plan!, gate.PublicHost, row, cancellationToken);
                await RecordAsync(gate.App!, "succeeded", null, cancellationToken, swap ? "swap" : "canary");
                return DeployOutcome.Ok("running");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var message = DeployFailureText.Describe(exception);
                _logger.LogError("Traffic shift failed for {ApplicationId}. {ExceptionType}", app.Id, exception.GetType().Name);
                await NotifyAsync(app, AlertKinds.DeployFailed, message, cancellationToken);
                return DeployOutcome.Fail(StatusCodes.Status502BadGateway, message);
            }
            finally
            {
                await ReleaseHeldAsync(app.Id, app.Environment, ownerId.Value);
            }
        }, cancellationToken);

    private async Task ApplySlotAsync(
        WorkloadSnapshot app,
        string endpointAddress,
        ComposePlan plan,
        string? publicHost,
        string slot,
        CancellationToken cancellationToken)
    {
        var scoped = await LoadScopedSecretsAsync(app, plan, cancellationToken);
        var endpoint = new DockerEndpoint(endpointAddress);
        var network = TrafficPlan.Network(app.Id, slot);
        var project = ComposeProjectName.Resolve(app.ComposeYaml, app.Name, app.Id);
        await _engine.EnsureNetworkAsync(endpoint, network, cancellationToken);
        if (plan.Services.Any(service => ComposePolicy.RouteHostname(service, publicHost) is not null))
        {
            await _edge.EnsureEdgeAsync(endpointAddress, cancellationToken);
        }

        await RemoveSlotContainersAsync(endpoint, app.Id, slot, cancellationToken);
        var ordered = ComposePolicy.StartOrder(plan.Services);
        var created = new List<string>();
        try
        {
            foreach (var service in ordered)
            {
                var image = service.Image;
                var auth = await _registries.ForImageAsync(image, cancellationToken);
                await _engine.PullImageAsync(endpoint, image, auth, cancellationToken);
                var injected = SecretInjection.Apply(service.Environment, service.Command, SecretInjection.ForService(service.Name, scoped));
                var labels = new Dictionary<string, string>
                {
                    ["cc.managed"] = "true",
                    [ManagedLabels.Application] = app.Id.ToString(),
                    [ManagedLabels.Service] = service.Name,
                    [ManagedLabels.Slot] = slot
                };
                ComposeProjectName.Stamp(labels, project, service.Name);
                var extra = new List<string>();
                var routeHost = ComposePolicy.RouteHostname(service, publicHost);
                if (routeHost is not null && service.Port is int port)
                {
                    var backend = TrafficPlan.BackendService(RouterName(app.Id, service.Name), slot);
                    foreach (var (key, value) in SlotRouteLabels.Backend(backend, port, _edge.EdgeNetworkName))
                    {
                        labels[key] = value;
                    }

                    extra.Add(_edge.EdgeNetworkName);
                }

                var id = await _engine.CreateContainerAsync(endpoint, new ContainerPlan(
                    ContainerName(app.Id, service.Name) + "-" + slot,
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
                created.Add(id);
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
        }
        catch (Exception)
        {
            foreach (var id in created)
            {
                try
                {
                    await _engine.RemoveContainerAsync(endpoint, id, CancellationToken.None);
                }
                catch (Exception cleanupError) when (cleanupError is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        "Removing slot container {ContainerId} after a failed start did not succeed. {ExceptionType}",
                        id,
                        cleanupError.GetType().Name);
                }
            }

            throw;
        }
    }

    private async Task RefreshDirectorsAsync(
        WorkloadSnapshot app,
        string endpointAddress,
        ComposePlan plan,
        string? publicHost,
        TrafficSlot row,
        CancellationToken cancellationToken)
    {
        var endpoint = new DockerEndpoint(endpointAddress);
        await RemoveSlotContainersAsync(endpoint, app.Id, ManagedLabels.RouteSlot, cancellationToken);
        var exposed = plan.Services
            .Select(service => (service, host: ComposePolicy.RouteHostname(service, publicHost)))
            .Where(item => item.host is not null && item.service.Port is int)
            .ToArray();
        if (exposed.Length == 0)
        {
            return;
        }

        await _edge.EnsureEdgeAsync(endpointAddress, cancellationToken);
        await _engine.PullImageAsync(endpoint, TrafficPlan.DirectorImage, null, cancellationToken);
        foreach (var (service, host) in exposed)
        {
            var router = RouterName(app.Id, service.Name);
            var weights = TrafficPlan.Weights(router, row.LiveSlot, row.CandidateSlot, row.CandidatePercent);
            var labels = new Dictionary<string, string>
            {
                ["cc.managed"] = "true",
                [ManagedLabels.Application] = app.Id.ToString(),
                [ManagedLabels.Service] = service.Name,
                [ManagedLabels.Slot] = ManagedLabels.RouteSlot,
                [ManagedLabels.Role] = ManagedLabels.SlotRouterRole
            };
            foreach (var (key, value) in SlotRouteLabels.Director(router, host!, _edge.UseTls, _edge.EdgeNetworkName))
            {
                labels[key] = value;
            }

            var proxy = SlotRouteLabels.ProxyConfig(weights.Select(weight => (
                Host: UpstreamHost(app.Id, service.Name, weight.Slot),
                Port: service.Port!.Value,
                weight.Weight)).ToArray());
            var id = await _engine.CreateContainerAsync(endpoint, new ContainerPlan(
                ContainerName(app.Id, service.Name) + "-route",
                TrafficPlan.DirectorImage,
                SlotRouteLabels.ProxyCommand,
                labels,
                new Dictionary<string, string> { [SlotRouteLabels.ProxyEnvironment] = proxy },
                _edge.EdgeNetworkName,
                "r" + app.Id.ToString("N")[..8],
                [],
                [],
                new Dictionary<string, string>(),
                "unless-stopped"), cancellationToken);
            await _engine.StartContainerAsync(endpoint, id, cancellationToken);
        }
    }

    private async Task<ScopedSecret[]> LoadScopedSecretsAsync(WorkloadSnapshot app, ComposePlan plan, CancellationToken cancellationToken)
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

        var serviceNames = plan.Services.Select(service => service.Name).ToHashSet(StringComparer.Ordinal);
        var targeted = catalog
            .Where(secret => secret.ServiceNames.Any(name => serviceNames.Contains(name)))
            .ToArray();
        var secretValues = new Dictionary<string, (string Value, string Mode)>(StringComparer.Ordinal);
        foreach (var secret in targeted)
        {
                var value = await _secretStore.ReadAsync(new ContainerControl.Modules.Applications.Secrets.SecretAddress(app.Environment, secret.Path), cancellationToken);
            if (value is null)
            {
                throw new DockerEngineException("A secret could not be read.");
            }

            secretValues[secret.Name] = (value, secret.InjectionMode);
        }

        return targeted
            .Select(secret => new ScopedSecret(secret.Name, secretValues[secret.Name].Value, secretValues[secret.Name].Mode, secret.ServiceNames))
            .ToArray();
    }

    private async Task RemoveSlotContainersAsync(DockerEndpoint endpoint, Guid applicationId, string slot, CancellationToken cancellationToken)
    {
        var existing = await _engine.ListByLabelAsync(endpoint, ManagedLabels.Application + "=" + applicationId, cancellationToken);
        foreach (var container in existing)
        {
            if (!string.Equals(container.Label(ManagedLabels.Slot), slot, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                await _engine.RemoveContainerAsync(endpoint, container.Id, cancellationToken);
            }
            catch (Docker.DotNet.DockerApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }
    }

    private static string UpstreamHost(Guid appId, string service, string slot)
    {
        var name = ContainerName(appId, service);
        return string.Equals(slot, TrafficPlan.Classic, StringComparison.Ordinal) ? name : name + "-" + slot;
    }

    private static bool IsBackend(EngineContainer container) =>
        !string.Equals(container.Label(ManagedLabels.Slot), ManagedLabels.RouteSlot, StringComparison.Ordinal)
        && !string.Equals(container.Label(ManagedLabels.Role), ManagedLabels.SlotRouterRole, StringComparison.Ordinal);

    private static bool MatchesSlot(EngineContainer container, string slot)
    {
        var actual = container.Label(ManagedLabels.Slot);
        if (string.Equals(slot, TrafficPlan.Classic, StringComparison.Ordinal))
        {
            return string.IsNullOrEmpty(actual);
        }

        return string.Equals(actual, slot, StringComparison.Ordinal);
    }

    private const string ExposedRequired = "Blue/green needs an exposed service with a hostname.";
}
