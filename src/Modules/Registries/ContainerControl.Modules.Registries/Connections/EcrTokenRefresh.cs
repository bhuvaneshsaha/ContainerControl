using System.Text;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Registries.Persistence;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Registries.Connections;

public sealed record EcrDockerToken(string Username, string Password, DateTimeOffset ExpiresAtUtc);

public sealed class EcrAuthorizer
{
    public async Task<EcrDockerToken> AuthorizeAsync(
        string region,
        string accessKeyId,
        string secretAccessKey,
        CancellationToken cancellationToken)
    {
        using var client = new AmazonECRClient(accessKeyId, secretAccessKey, RegionEndpoint.GetBySystemName(region));
        var response = await client.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest(), cancellationToken);
        var data = response.AuthorizationData?.FirstOrDefault();
        if (data is null || string.IsNullOrWhiteSpace(data.AuthorizationToken))
        {
            throw new DockerEngineException("ECR did not return a pull token.");
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(data.AuthorizationToken));
        var split = decoded.IndexOf(':');
        if (split <= 0)
        {
            throw new DockerEngineException("ECR did not return a pull token.");
        }

        var expires = data.ExpiresAt.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(data.ExpiresAt.Value, DateTimeKind.Utc))
            : DateTimeOffset.UtcNow.AddHours(12);
        return new EcrDockerToken(decoded[..split], decoded[(split + 1)..], expires);
    }
}

public sealed class EcrTokenRefresh : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EcrTokenRefresh> _logger;

    public EcrTokenRefresh(IServiceScopeFactory scopes, ILogger<EcrTokenRefresh> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        do
        {
            await RefreshDueAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RefreshDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistriesDbContext>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretStore>();
        var ecr = scope.ServiceProvider.GetRequiredService<EcrAuthorizer>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var due = await db.Connections.Where(connection => connection.Kind == "Ecr").ToListAsync(cancellationToken);
        foreach (var connection in due.Where(connection => RegistryImage.IsDue(connection.EcrTokenExpiresAtUtc, clock.UtcNow)))
        {
            if (connection.AccessKeyPath is null || connection.SecretKeyPath is null || RegistryImage.EcrRegion(connection.Server) is not { } region)
            {
                continue;
            }

            try
            {
                var accessKey = await secrets.ReadAsync(new SecretAddress(connection.Environment, connection.AccessKeyPath), cancellationToken);
                var secretKey = await secrets.ReadAsync(new SecretAddress(connection.Environment, connection.SecretKeyPath), cancellationToken);
                if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                {
                    throw new DockerEngineException("A registry credential could not be read.");
                }

                var token = await ecr.AuthorizeAsync(region, accessKey, secretKey, cancellationToken);
                await secrets.WriteAsync(new SecretAddress(connection.Environment, connection.UsernamePath), token.Username, cancellationToken);
                await secrets.WriteAsync(new SecretAddress(connection.Environment, connection.PasswordPath), token.Password, cancellationToken);
                connection.EcrTokenExpiresAtUtc = token.ExpiresAtUtc;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError("ECR token refresh failed. {ExceptionType} {RegistryId}", exception.GetType().Name, connection.Id);
            }
        }
    }
}
