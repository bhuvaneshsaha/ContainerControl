using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Registries.Persistence;
using ContainerControl.SharedKernel.Auditing;
using ContainerControl.SharedKernel.CurrentUser;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Modules.Registries.Connections;

public interface IRegistryLogin
{
    Task<ImagePullAuth?> ForImageAsync(string image, CancellationToken cancellationToken);
}

public sealed class RegistryLogin : IRegistryLogin
{
    private readonly RegistriesDbContext _db;
    private readonly ISecretStore _secrets;

    public RegistryLogin(RegistriesDbContext db, ISecretStore secrets)
    {
        _db = db;
        _secrets = secrets;
    }

    public async Task<ImagePullAuth?> ForImageAsync(string image, CancellationToken cancellationToken)
    {
        var connections = await _db.Connections.AsNoTracking().ToListAsync(cancellationToken);
        var match = connections.FirstOrDefault(connection => RegistryImage.Matches(image, connection.Server, connection.Kind));
        if (match is null)
        {
            return null;
        }

        var username = await _secrets.ReadAsync(new SecretAddress(match.Environment, match.UsernamePath), cancellationToken);
        var password = await _secrets.ReadAsync(new SecretAddress(match.Environment, match.PasswordPath), cancellationToken);
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new DockerEngineException("A registry credential could not be read.");
        }

        return new ImagePullAuth(match.Server, username, password);
    }
}

public sealed class RegistryAdmin
{
    private readonly RegistriesDbContext _db;
    private readonly ISecretStore _secrets;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly EcrAuthorizer _ecr;

    public RegistryAdmin(
        RegistriesDbContext db,
        ISecretStore secrets,
        IAuditSink audit,
        ICurrentUser currentUser,
        IClock clock,
        EcrAuthorizer ecr)
    {
        _db = db;
        _secrets = secrets;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
        _ecr = ecr;
    }

    public async Task<IReadOnlyList<RegistryConnection>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Connections.AsNoTracking().OrderBy(connection => connection.Name).ToListAsync(cancellationToken);

    public async Task<(bool Ok, Guid? Id, string? Error)> CreateAsync(
        string name,
        string kind,
        string server,
        string environment,
        string? username,
        string? password,
        string? accessKeyId,
        string? secretAccessKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || !RegistryKinds.TryNormalize(kind, out var normalized))
        {
            return (false, null, "Enter a name and a type of Acr, Ecr, DockerHub, or Harbor.");
        }

        if (server.Contains("registry:2", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, "registry:2 is not a supported registry.");
        }

        var host = string.IsNullOrWhiteSpace(server)
            ? normalized == "DockerHub" ? "docker.io" : string.Empty
            : server.Trim();
        if (host.Length == 0 || host.Contains("://", StringComparison.Ordinal))
        {
            return (false, null, "Enter the registry host, such as myregistry.azurecr.io.");
        }

        if (environment is not ("dev" or "staging" or "prod"))
        {
            return (false, null, "Enter an Infisical environment of dev, staging, or prod.");
        }

        if (normalized == "Ecr")
        {
            if (RegistryImage.EcrRegion(host) is null)
            {
                return (false, null, "An ECR host looks like account.dkr.ecr.region.amazonaws.com.");
            }

            if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey))
            {
                return (false, null, "Enter the ECR access key id and secret.");
            }
        }
        else if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return (false, null, "Enter the registry username and password.");
        }

        var id = Guid.NewGuid();
        var usernamePath = $"registry-{id:N}-username";
        var passwordPath = $"registry-{id:N}-password";
        string? accessKeyPath = null;
        string? secretKeyPath = null;
        if (normalized == "Ecr")
        {
            accessKeyPath = $"registry-{id:N}-access-key";
            secretKeyPath = $"registry-{id:N}-secret-key";
            await _secrets.WriteAsync(new SecretAddress(environment, accessKeyPath), accessKeyId!.Trim(), cancellationToken);
            await _secrets.WriteAsync(new SecretAddress(environment, secretKeyPath), secretAccessKey!, cancellationToken);
            var region = RegistryImage.EcrRegion(host)!;
            var token = await _ecr.AuthorizeAsync(region, accessKeyId.Trim(), secretAccessKey!, cancellationToken);
            await _secrets.WriteAsync(new SecretAddress(environment, usernamePath), token.Username, cancellationToken);
            await _secrets.WriteAsync(new SecretAddress(environment, passwordPath), token.Password, cancellationToken);
            _db.Connections.Add(new RegistryConnection
            {
                Id = id,
                Name = name.Trim(),
                Kind = normalized,
                Server = host,
                Environment = environment,
                UsernamePath = usernamePath,
                PasswordPath = passwordPath,
                AccessKeyPath = accessKeyPath,
                SecretKeyPath = secretKeyPath,
                EcrTokenExpiresAtUtc = token.ExpiresAtUtc,
                CreatedAtUtc = _clock.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(new AuditRecord("registries.connection.created", "registry", id.ToString(), _currentUser.UserId), cancellationToken);
            return (true, id, null);
        }
        else
        {
            await _secrets.WriteAsync(new SecretAddress(environment, usernamePath), username!.Trim(), cancellationToken);
            await _secrets.WriteAsync(new SecretAddress(environment, passwordPath), password!, cancellationToken);
        }

        var connection = new RegistryConnection
        {
            Id = id,
            Name = name.Trim(),
            Kind = normalized,
            Server = host,
            Environment = environment,
            UsernamePath = usernamePath,
            PasswordPath = passwordPath,
            AccessKeyPath = accessKeyPath,
            SecretKeyPath = secretKeyPath,
            CreatedAtUtc = _clock.UtcNow
        };
        _db.Connections.Add(connection);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditRecord("registries.connection.created", "registry", connection.Id.ToString(), _currentUser.UserId), cancellationToken);
        return (true, id, null);
    }
}
