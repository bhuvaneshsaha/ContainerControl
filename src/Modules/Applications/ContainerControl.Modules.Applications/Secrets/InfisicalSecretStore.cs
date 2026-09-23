using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ContainerControl.Modules.Applications.Secrets;

public sealed class InfisicalSecretStore : ISecretStore
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public InfisicalSecretStore(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public Task WriteAsync(SecretAddress address, string value, CancellationToken cancellationToken) =>
        SendAsync(address, HttpMethod.Post, value, cancellationToken);

    public Task DeleteAsync(SecretAddress address, CancellationToken cancellationToken) =>
        SendAsync(address, HttpMethod.Delete, null, cancellationToken);

    public async Task<string?> ReadAsync(SecretAddress address, CancellationToken cancellationToken)
    {
        var credential = CredentialFor(address.Environment);
        var token = await LoginAsync(credential, cancellationToken);
        var name = SecretName(address.Path);
        var client = _httpClientFactory.CreateClient(nameof(InfisicalSecretStore));
        var uri = $"{credential.SiteUrl.TrimEnd('/')}/api/v4/secrets/{Uri.EscapeDataString(name)}?projectId={Uri.EscapeDataString(credential.ProjectId)}&environment={Uri.EscapeDataString(address.Environment)}&secretPath=/";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SecretStoreException("Infisical did not return the secret.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.TryGetProperty("secret", out var secret)
            && secret.TryGetProperty("secretValue", out var secretValue))
        {
            return secretValue.GetString();
        }

        if (document.RootElement.TryGetProperty("secretValue", out var direct))
        {
            return direct.GetString();
        }

        return null;
    }

    private async Task SendAsync(SecretAddress address, HttpMethod method, string? value, CancellationToken cancellationToken)
    {
        var credential = CredentialFor(address.Environment);
        var token = await LoginAsync(credential, cancellationToken);
        var name = SecretName(address.Path);
        var client = _httpClientFactory.CreateClient(nameof(InfisicalSecretStore));
        var uri = $"{credential.SiteUrl.TrimEnd('/')}/api/v4/secrets/{Uri.EscapeDataString(name)}";
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var payload = new Dictionary<string, string>
        {
            ["projectId"] = credential.ProjectId,
            ["environment"] = address.Environment,
            ["secretPath"] = "/",
            ["type"] = "shared"
        };
        if (value is not null)
        {
            payload["secretValue"] = value;
        }

        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SecretStoreException("Infisical did not accept the secret change.");
        }
    }

    private async Task<string> LoginAsync(InfisicalCredential credential, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(InfisicalSecretStore));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{credential.SiteUrl.TrimEnd('/')}/api/v1/auth/universal-auth/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["clientId"] = credential.ClientId,
                ["clientSecret"] = credential.ClientSecret
            })
        };
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SecretStoreException("Infisical rejected the machine identity.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var token = document.RootElement.TryGetProperty("accessToken", out var accessToken)
            ? accessToken.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecretStoreException("Infisical did not return an access token.");
        }

        return token;
    }

    private InfisicalCredential CredentialFor(string environment)
    {
        var siteUrl = _configuration["Infisical:SiteUrl"];
        var section = _configuration.GetSection($"Infisical:Environments:{environment}");
        var clientId = section["ClientId"];
        var clientSecret = section["ClientSecret"];
        var projectId = section["ProjectId"];
        if (string.IsNullOrWhiteSpace(siteUrl)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(projectId))
        {
            throw new SecretStoreException(
                "Infisical machine credentials for this environment are not configured. Create the identity and set the host environment variables. See docs/production-setup.md.");
        }

        return new InfisicalCredential(siteUrl, clientId, clientSecret, projectId);
    }

    private static string SecretName(string path)
    {
        var name = path.Trim().Trim('/');
        var slash = name.LastIndexOf('/');
        return slash >= 0 ? name[(slash + 1)..] : name;
    }

    private sealed record InfisicalCredential(string SiteUrl, string ClientId, string ClientSecret, string ProjectId);
}

public sealed class SecretStoreException : Exception
{
    public SecretStoreException(string message)
        : base(message)
    {
    }
}
