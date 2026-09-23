using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ContainerControl.Modules.Delivery.Runs;

public sealed class DeployService
{
    private readonly DeliveryDbContext _db;
    private readonly IWorkloadStore _apps;
    private readonly IDockerHostLookup _hosts;
    private readonly IDockerEngine _engine;
    private readonly ISecretCatalog _secretCatalog;
    private readonly ISecretStore _secretStore;
    private readonly IEdgeGateway _edge;
    private readonly ITeamDirectory _teams;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IConfiguration _configuration;

    public DeployService(
        DeliveryDbContext db,
        IWorkloadStore apps,
        IDockerHostLookup hosts,
        IDockerEngine engine,
        ISecretCatalog secretCatalog,
        ISecretStore secretStore,
        IEdgeGateway edge,
        ITeamDirectory teams,
        ICurrentUser currentUser,
        IClock clock,
        IConfiguration configuration)
    {
        _db = db;
        _apps = apps;
        _hosts = hosts;
        _engine = engine;
        _secretCatalog = secretCatalog;
        _secretStore = secretStore;
        _edge = edge;
        _teams = teams;
        _currentUser = currentUser;
        _clock = clock;
        _configuration = configuration;
    }

    public async Task<DeployOutcome> DeployAsync(Guid applicationId, Guid actorUserId, bool approved, CancellationToken cancellationToken)
    {
        if (!await MemberAsync(applicationId, actorUserId, cancellationToken))
        {
            return DeployOutcome.NotFound();
        }

        if (!await TryLeaseAsync(cancellationToken))
        {
            return DeployOutcome.Fail(StatusCodes.Status409Conflict, "A deployment is already running.");
        }

        try
        {
            return await DeployHeldAsync(applicationId, approved, cancellationToken);
        }
        finally
        {
            await ReleaseLeaseAsync(cancellationToken);
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

        await _apps.ReplaceDesiredAsync(applicationId, new DesiredState(
            previous.Image,
            previous.CommandJson,
            previous.ComposeYaml,
            previous.InternalPort,
            previous.Hostname,
            previous.Exposed), cancellationToken);
        return await DeployAsync(applicationId, _currentUser.UserId ?? Guid.Empty, approved: true, cancellationToken);
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

    private async Task<DeployOutcome> DeployHeldAsync(Guid applicationId, bool approved, CancellationToken cancellationToken)
    {
        var app = await _apps.FindAsync(applicationId, cancellationToken);
        if (app is null)
        {
            return DeployOutcome.NotFound();
        }

        if (app.RequiresApproval && !approved)
        {
            await RecordAsync(app, "pending-approval", null, cancellationToken);
            await _apps.SetStatusAsync(app.Id, "pending-approval", cancellationToken);
            return DeployOutcome.Ok("pending-approval");
        }

        var plan = string.IsNullOrWhiteSpace(app.ComposeYaml)
            ? ComposePolicy.FromImage(app.Image ?? string.Empty, ReadCommand(app.CommandJson), app.Exposed, app.InternalPort)
            : ComposePolicy.Parse(app.ComposeYaml);
        if (!plan.Accepted)
        {
            await RecordAsync(app, "rejected", string.Join(" ", plan.Errors), cancellationToken);
            await _apps.SetStatusAsync(app.Id, "rejected", cancellationToken);
            return DeployOutcome.Fail(StatusCodes.Status400BadRequest, string.Join(" ", plan.Errors));
        }

        var anyExposed = plan.Services.Any(service => service.Exposed) || (string.IsNullOrWhiteSpace(app.ComposeYaml) && app.Exposed);
        if (anyExposed && !await _edge.HostnameAllowedAsync(app.Hostname, cancellationToken))
        {
            const string message = "The hostname is not under an allowed domain.";
            await RecordAsync(app, "rejected", message, cancellationToken);
            return DeployOutcome.Fail(StatusCodes.Status400BadRequest, message);
        }

        var host = await _hosts.FindAsync(app.HostId, cancellationToken);
        if (host is null)
        {
            return DeployOutcome.Fail(StatusCodes.Status404NotFound, "The Docker host is gone.");
        }

        try
        {
            await ApplyAsync(app, host.Endpoint, plan, anyExposed, cancellationToken);
        }
        catch (Exception exception) when (exception is DockerEngineException or Docker.DotNet.DockerApiException or SecretStoreException or HttpRequestException or IOException)
        {
            await RecordAsync(app, "failed", "The Engine rejected the deployment.", cancellationToken);
            await _apps.SetStatusAsync(app.Id, "failed", cancellationToken);
            return DeployOutcome.Fail(StatusCodes.Status502BadGateway, "The Engine rejected the deployment.");
        }

        await RecordAsync(app, "succeeded", null, cancellationToken);
        await _apps.SetStatusAsync(app.Id, "running", cancellationToken);
        return DeployOutcome.Ok("running");
    }

    private async Task ApplyAsync(WorkloadSnapshot app, string endpointAddress, ComposePlan plan, bool anyExposed, CancellationToken cancellationToken)
    {
        var endpoint = new DockerEndpoint(endpointAddress);
        var network = "cc-app-" + app.Id.ToString("N");
        await _engine.EnsureNetworkAsync(endpoint, network, cancellationToken);
        if (anyExposed)
        {
            await _edge.EnsureEdgeAsync(endpointAddress, cancellationToken);
        }

        var secrets = await _secretCatalog.ListAsync(app.TeamId, app.Environment, cancellationToken);
        var secretValues = new Dictionary<string, (string Value, string Mode)>(StringComparer.Ordinal);
        foreach (var secret in secrets)
        {
            var value = await _secretStore.ReadAsync(new SecretAddress(app.Environment, secret.Path), cancellationToken);
            if (value is null)
            {
                throw new DockerEngineException("A secret could not be read.");
            }

            secretValues[secret.Name] = (value, secret.InjectionMode);
        }

        var existing = await _engine.ListByLabelAsync(endpoint, "cc.application=" + app.Id, cancellationToken);
        foreach (var container in existing)
        {
            await _engine.RemoveContainerAsync(endpoint, container.Id, cancellationToken);
        }

        var ordered = Order(plan.Services);
        foreach (var service in ordered)
        {
            var image = service.Image;
            await _engine.PullImageAsync(endpoint, image, cancellationToken);
            var environment = new Dictionary<string, string>(service.Environment, StringComparer.Ordinal);
            var binds = new List<string>();
            foreach (var (name, secret) in secretValues)
            {
                if (secret.Mode == "file")
                {
                    binds.Add(WriteSecretFile(app.Id, name, secret.Value));
                }
                else
                {
                    environment[name] = secret.Value;
                }
            }

            var exposed = service.Exposed;
            var labels = new Dictionary<string, string>
            {
                ["cc.managed"] = "true",
                ["cc.application"] = app.Id.ToString(),
                ["cc.service"] = service.Name
            };
            var extra = new List<string>();
            if (exposed && !string.IsNullOrWhiteSpace(app.Hostname) && service.Port is not null)
            {
                foreach (var (key, value) in _edge.LabelsFor(RouterName(app.Id, service.Name), app.Hostname, service.Port.Value))
                {
                    labels[key] = value;
                }

                extra.Add(_edge.EdgeNetworkName);
            }

            var id = await _engine.CreateContainerAsync(endpoint, new ContainerPlan(
                ContainerName(app.Id, service.Name),
                image,
                service.Command,
                labels,
                environment,
                network,
                service.Name,
                extra,
                binds,
                new Dictionary<string, string>(),
                "unless-stopped"), cancellationToken);
            await _engine.StartContainerAsync(endpoint, id, cancellationToken);
        }
    }

    private string WriteSecretFile(Guid appId, string name, string value)
    {
        var root = _configuration["Secrets:FileRoot"] ?? "/tmp/containercontrol-secrets";
        var directory = Path.Combine(root, appId.ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, value);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return path + ":/run/secrets/" + name + ":ro";
    }

    private async Task RecordAsync(WorkloadSnapshot app, string status, string? error, CancellationToken cancellationToken)
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
            Error = error,
            CreatedAtUtc = _clock.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TryLeaseAsync(CancellationToken cancellationToken)
    {
        var lease = await _db.Leases.SingleAsync(item => item.Id == 1, cancellationToken);
        if (lease.ExpiresAtUtc is not null && lease.ExpiresAtUtc > _clock.UtcNow && lease.OwnerId is not null)
        {
            return false;
        }

        lease.OwnerId = Guid.NewGuid();
        lease.ExpiresAtUtc = _clock.UtcNow.AddMinutes(5);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private async Task ReleaseLeaseAsync(CancellationToken cancellationToken)
    {
        var lease = await _db.Leases.SingleAsync(item => item.Id == 1, cancellationToken);
        lease.OwnerId = null;
        lease.ExpiresAtUtc = null;
        await _db.SaveChangesAsync(cancellationToken);
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

    private static IReadOnlyList<PlannedService> Order(IReadOnlyList<PlannedService> services)
    {
        var pending = services.ToList();
        var ordered = new List<PlannedService>();
        while (pending.Count > 0)
        {
            var next = pending.FirstOrDefault(service => service.DependsOn.All(name => ordered.Any(done => done.Name == name)))
                ?? pending[0];
            ordered.Add(next);
            pending.Remove(next);
        }

        return ordered;
    }

    private static IReadOnlyList<string>? ReadCommand(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<string>>(json);
    }

    private static string ContainerName(Guid appId, string service) =>
        "cc" + appId.ToString("N")[..12] + "-" + service;

    private static string RouterName(Guid appId, string service) =>
        "cc" + appId.ToString("N")[..12] + service.Replace("-", "");
}

public sealed record DeployOutcome(bool Succeeded, int StatusCode, string Status, string? Error)
{
    public static DeployOutcome Ok(string status) => new(true, StatusCodes.Status200OK, status, null);

    public static DeployOutcome NotFound() => new(false, StatusCodes.Status404NotFound, "not-found", "The application was not found.");

    public static DeployOutcome Fail(int statusCode, string error) => new(false, statusCode, "failed", error);
}
