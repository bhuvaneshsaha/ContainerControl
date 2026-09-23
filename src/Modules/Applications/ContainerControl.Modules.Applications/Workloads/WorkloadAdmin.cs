using System.Text.Json;
using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Persistence;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Applications.Workloads;

public sealed class WorkloadAdmin : IWorkloadStore, ISecretCatalog
{
    private static readonly HashSet<string> Environments = new(StringComparer.Ordinal) { "dev", "staging", "prod" };

    private readonly ApplicationsDbContext _db;
    private readonly ISecretStore _secrets;
    private readonly ITeamDirectory _teams;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public WorkloadAdmin(
        ApplicationsDbContext db,
        ISecretStore secrets,
        ITeamDirectory teams,
        ICurrentUser currentUser,
        IClock clock,
        IAuditSink audit)
    {
        _db = db;
        _secrets = secrets;
        _teams = teams;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ContainerApp>> ListForActorAsync(CancellationToken cancellationToken)
    {
        var teamIds = await _teams.TeamIdsForAsync(_currentUser.UserId ?? Guid.Empty, cancellationToken);
        return await _db.Apps
            .AsNoTracking()
            .Where(app => teamIds.Contains(app.TeamId))
            .OrderBy(app => app.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContainerApp?> FindEntityAsync(Guid id, CancellationToken cancellationToken)
    {
        var app = await _db.Apps.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (app is null || !await CanSeeAsync(app.TeamId, cancellationToken))
        {
            return null;
        }

        return app;
    }

    public async Task<WorkloadSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var app = await _db.Apps.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return app is null ? null : ToSnapshot(app);
    }

    public async Task SetStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var app = await _db.Apps.SingleAsync(item => item.Id == id, cancellationToken);
        app.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceDesiredAsync(Guid id, DesiredState desired, CancellationToken cancellationToken)
    {
        var app = await _db.Apps.SingleAsync(item => item.Id == id, cancellationToken);
        app.Image = desired.Image;
        app.CommandJson = desired.CommandJson;
        app.ComposeYaml = desired.ComposeYaml;
        app.InternalPort = desired.InternalPort;
        app.Hostname = desired.Hostname;
        app.Exposed = desired.Exposed;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(bool Ok, Guid? Id, string? Error)> CreateAsync(
        Guid teamId,
        Guid hostId,
        string name,
        string environment,
        string? image,
        IReadOnlyList<string>? command,
        string? composeYaml,
        int? internalPort,
        string? hostname,
        bool exposed,
        CancellationToken cancellationToken)
    {
        if (!await CanSeeAsync(teamId, cancellationToken))
        {
            return (false, null, "You are not a member of that team.");
        }

        if (string.IsNullOrWhiteSpace(name) || !Environments.Contains(environment))
        {
            return (false, null, "Enter an application name and an environment of dev, staging, or prod.");
        }

        if (string.IsNullOrWhiteSpace(image) && string.IsNullOrWhiteSpace(composeYaml))
        {
            return (false, null, "Enter an image or a compose file.");
        }

        var app = new ContainerApp
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            HostId = hostId,
            Name = name.Trim(),
            Environment = environment,
            Image = string.IsNullOrWhiteSpace(image) ? null : image.Trim(),
            CommandJson = command is null || command.Count == 0 ? null : JsonSerializer.Serialize(command),
            ComposeYaml = string.IsNullOrWhiteSpace(composeYaml) ? null : composeYaml,
            InternalPort = internalPort,
            Hostname = string.IsNullOrWhiteSpace(hostname) ? null : hostname.Trim().ToLowerInvariant(),
            Exposed = exposed,
            RequiresApproval = environment == "prod",
            Status = "registered",
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Apps.Add(app);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("apps.created", "application", app.Id.ToString(), _currentUser.UserId), cancellationToken);
        return (true, app.Id, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(
        Guid id,
        string? image,
        IReadOnlyList<string>? command,
        string? composeYaml,
        int? internalPort,
        string? hostname,
        bool exposed,
        CancellationToken cancellationToken)
    {
        var app = await _db.Apps.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (app is null || !await CanSeeAsync(app.TeamId, cancellationToken))
        {
            return (false, "not-found");
        }

        if (string.IsNullOrWhiteSpace(image) && string.IsNullOrWhiteSpace(composeYaml))
        {
            return (false, "Enter an image or a compose file.");
        }

        app.Image = string.IsNullOrWhiteSpace(image) ? null : image.Trim();
        app.CommandJson = command is null || command.Count == 0 ? null : JsonSerializer.Serialize(command);
        app.ComposeYaml = string.IsNullOrWhiteSpace(composeYaml) ? null : composeYaml;
        app.InternalPort = internalPort;
        app.Hostname = string.IsNullOrWhiteSpace(hostname) ? null : hostname.Trim().ToLowerInvariant();
        app.Exposed = exposed;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<SecretReference>> ListSecretsAsync(Guid teamId, string environment, CancellationToken cancellationToken)
    {
        if (!await CanSeeAsync(teamId, cancellationToken))
        {
            return [];
        }

        return await _db.Secrets
            .AsNoTracking()
            .Where(secret => secret.TeamId == teamId && secret.Environment == environment)
            .OrderBy(secret => secret.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SecretSnapshot>> ListAsync(Guid teamId, string environment, CancellationToken cancellationToken)
    {
        var secrets = await _db.Secrets
            .AsNoTracking()
            .Where(secret => secret.TeamId == teamId && secret.Environment == environment)
            .OrderBy(secret => secret.Name)
            .ToListAsync(cancellationToken);
        return secrets.Select(secret => new SecretSnapshot(secret.Name, secret.Path, secret.InjectionMode)).ToArray();
    }

    public async Task<(bool Ok, string? Error)> SaveSecretAsync(
        Guid teamId,
        string environment,
        string name,
        string injectionMode,
        string value,
        CancellationToken cancellationToken)
    {
        if (!await CanSeeAsync(teamId, cancellationToken))
        {
            return (false, "You are not a member of that team.");
        }

        if (!Environments.Contains(environment)
            || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(value)
            || injectionMode is not ("env" or "file"))
        {
            return (false, "Enter a name, an environment, an injection mode of env or file, and a value.");
        }

        var path = $"/teams/{teamId:N}/{name.Trim()}";
        var address = new SecretAddress(environment, path);
        await _secrets.WriteAsync(address, value, cancellationToken);
        var existing = await _db.Secrets.SingleOrDefaultAsync(
            secret => secret.TeamId == teamId && secret.Environment == environment && secret.Name == name.Trim(),
            cancellationToken);
        var created = existing is null;
        if (existing is null)
        {
            existing = new SecretReference
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                Environment = environment,
                Name = name.Trim(),
                Path = path,
                InjectionMode = injectionMode,
                CreatedAtUtc = _clock.UtcNow
            };
            _db.Secrets.Add(existing);
        }
        else
        {
            existing.InjectionMode = injectionMode;
            existing.Path = path;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            new AuditRecord(created ? "secrets.created" : "secrets.updated", "secret", path, _currentUser.UserId),
            cancellationToken);
        return (true, null);
    }

    public async Task<SecretReference?> FindSecretAsync(Guid id, CancellationToken cancellationToken)
    {
        var secret = await _db.Secrets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (secret is null || !await CanSeeAsync(secret.TeamId, cancellationToken))
        {
            return null;
        }

        return secret;
    }

    public async Task<bool> DeleteSecretAsync(Guid id, CancellationToken cancellationToken)
    {
        var secret = await _db.Secrets.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (secret is null || !await CanSeeAsync(secret.TeamId, cancellationToken))
        {
            return false;
        }

        await _secrets.DeleteAsync(new SecretAddress(secret.Environment, secret.Path), cancellationToken);
        _db.Secrets.Remove(secret);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("secrets.deleted", "secret", secret.Path, _currentUser.UserId), cancellationToken);
        return true;
    }

    private async Task<bool> CanSeeAsync(Guid teamId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
        {
            return false;
        }

        return await _teams.IsMemberAsync(_currentUser.UserId.Value, teamId, cancellationToken);
    }

    private static WorkloadSnapshot ToSnapshot(ContainerApp app) =>
        new(
            app.Id,
            app.TeamId,
            app.HostId,
            app.Name,
            app.Environment,
            app.Image,
            app.CommandJson,
            app.ComposeYaml,
            app.InternalPort,
            app.Hostname,
            app.Exposed,
            app.RequiresApproval,
            app.Status);
}
