using System.Text.Json;
using ContainerControl.Modules.Access.Application.Teams;
using ContainerControl.Modules.Applications.Persistence;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Hostnames;
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
        if (!PublicHostname.TryCanonical(desired.Hostname, out var hostname, out var hostnameError))
        {
            throw new ArgumentException(hostnameError ?? PublicHostname.InvalidMessage);
        }

        var app = await _db.Apps.SingleAsync(item => item.Id == id, cancellationToken);
        app.Image = desired.Image;
        app.CommandJson = desired.CommandJson;
        app.ComposeYaml = desired.ComposeYaml;
        app.InternalPort = desired.InternalPort;
        app.Hostname = hostname;
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
        bool requiresApproval,
        bool allowDatabaseImages,
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

        if (!PublicHostname.TryCanonical(hostname, out var canonicalHostname, out var hostnameError))
        {
            return (false, null, hostnameError);
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
            Hostname = canonicalHostname,
            Exposed = exposed,
            RequiresApproval = ApprovalPolicy.Required(environment, requiresApproval),
            AllowDatabaseImages = allowDatabaseImages,
            Status = "registered",
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Apps.Add(app);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("apps.created", "application", app.Id.ToString(), _currentUser.UserId), cancellationToken);
        if (allowDatabaseImages)
        {
            await WriteDatabaseImageAuditAsync(app.Id, allowed: true, cancellationToken);
        }

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
        bool requiresApproval,
        bool allowDatabaseImages,
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

        if (!PublicHostname.TryCanonical(hostname, out var canonicalHostname, out var hostnameError))
        {
            return (false, hostnameError);
        }

        app.Image = string.IsNullOrWhiteSpace(image) ? null : image.Trim();
        app.CommandJson = command is null || command.Count == 0 ? null : JsonSerializer.Serialize(command);
        app.ComposeYaml = string.IsNullOrWhiteSpace(composeYaml) ? null : composeYaml;
        app.InternalPort = internalPort;
        app.Hostname = canonicalHostname;
        var databaseImagesChanged = app.AllowDatabaseImages != allowDatabaseImages;
        app.Exposed = exposed;
        app.RequiresApproval = ApprovalPolicy.Required(app.Environment, requiresApproval);
        app.AllowDatabaseImages = allowDatabaseImages;
        await _db.SaveChangesAsync(cancellationToken);
        if (databaseImagesChanged)
        {
            await WriteDatabaseImageAuditAsync(app.Id, allowDatabaseImages, cancellationToken);
        }

        return (true, null);
    }

    public async Task<IReadOnlyList<SecretReference>> ListSecretsAsync(Guid teamId, string environment, CancellationToken cancellationToken)
    {
        if (!await CanSeeAsync(teamId, cancellationToken))
        {
            return [];
        }

        return await SecretsQuery(teamId, environment).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SecretSnapshot>> ListAsync(Guid teamId, string environment, CancellationToken cancellationToken)
    {
        var secrets = await SecretsQuery(teamId, environment).ToListAsync(cancellationToken);
        return secrets
            .Select(secret => new SecretSnapshot(secret.Name, secret.Path, secret.InjectionMode, secret.OrderedServiceNames()))
            .ToArray();
    }

    public async Task<(bool Ok, string? Error)> SaveSecretAsync(
        Guid teamId,
        string environment,
        string name,
        string injectionMode,
        string value,
        IReadOnlyList<string>? serviceNames,
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

        if (!TryNormalizeServices(serviceNames, out var services, out var serviceError))
        {
            return (false, serviceError);
        }

        var path = $"/teams/{teamId:N}/{name.Trim()}";
        var address = new SecretAddress(environment, path);
        try
        {
            await _secrets.WriteAsync(address, value, cancellationToken);
        }
        catch (SecretStoreException exception)
        {
            return (false, exception.Message);
        }
        var existing = await _db.Secrets
            .Include(secret => secret.Targets)
            .SingleOrDefaultAsync(
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

        var desired = services.ToHashSet(StringComparer.Ordinal);
        existing.Targets.RemoveAll(target => !desired.Contains(target.ServiceName));
        foreach (var serviceName in services)
        {
            if (existing.Targets.All(target => target.ServiceName != serviceName))
            {
                existing.Targets.Add(new SecretServiceTarget { SecretId = existing.Id, ServiceName = serviceName });
            }
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

        try
        {
            await _secrets.DeleteAsync(new SecretAddress(secret.Environment, secret.Path), cancellationToken);
        }
        catch (SecretStoreException)
        {
            return false;
        }
        _db.Secrets.Remove(secret);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("secrets.deleted", "secret", secret.Path, _currentUser.UserId), cancellationToken);
        return true;
    }

    private Task WriteDatabaseImageAuditAsync(Guid appId, bool allowed, CancellationToken cancellationToken) =>
        _audit.WriteAsync(
            new AuditRecord(
                allowed ? "apps.database-images.allowed" : "apps.database-images.blocked",
                "application",
                appId.ToString(),
                _currentUser.UserId),
            cancellationToken);

    private IQueryable<SecretReference> SecretsQuery(Guid teamId, string environment) =>
        _db.Secrets
            .AsNoTracking()
            .Include(secret => secret.Targets)
            .Where(secret => secret.TeamId == teamId && secret.Environment == environment)
            .OrderBy(secret => secret.Name);

    private static bool TryNormalizeServices(IReadOnlyList<string>? serviceNames, out string[] normalized, out string? error)
    {
        normalized = [];
        if (serviceNames is null || serviceNames.Count == 0)
        {
            error = "Select at least one service for this secret.";
            return false;
        }

        if (serviceNames.Count > 32)
        {
            error = "Select at most 32 services for this secret.";
            return false;
        }

        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var raw in serviceNames)
        {
            var serviceName = raw?.Trim() ?? string.Empty;
            if (!IsServiceName(serviceName))
            {
                error = "Service names use letters, digits, dots, underscores, and hyphens.";
                return false;
            }

            names.Add(serviceName);
        }

        normalized = names.ToArray();
        error = null;
        return true;
    }

    private static bool IsServiceName(string name)
    {
        if (name.Length is 0 or > 63)
        {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(name[0]))
        {
            return false;
        }

        for (var index = 1; index < name.Length; index++)
        {
            var character = name[index];
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

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
            app.Status,
            app.AllowDatabaseImages);
}
