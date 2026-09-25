using System.Security.Cryptography.X509Certificates;

namespace ContainerControl.Modules.Platform.Engine;

public interface IEngineSecretReader
{
    Task<string?> ReadAsync(string environment, string path, CancellationToken cancellationToken);
}

public interface IEngineCertificateLoader
{
    Task<EngineCertificateMaterial> LoadAsync(string certRef, string keyRef, string caRef, CancellationToken cancellationToken);
}

public sealed class EngineCertificateMaterial : IDisposable
{
    public EngineCertificateMaterial(X509Certificate2 client, X509Certificate2 ca)
    {
        Client = client;
        Ca = ca;
    }

    public X509Certificate2 Client { get; }

    public X509Certificate2 Ca { get; }

    public void Dispose()
    {
        Client.Dispose();
        Ca.Dispose();
    }
}

internal sealed class UnavailableEngineSecretReader : IEngineSecretReader
{
    public Task<string?> ReadAsync(string environment, string path, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}

public sealed class EngineCertificateLoader(IEngineSecretReader secrets) : IEngineCertificateLoader
{
    public async Task<EngineCertificateMaterial> LoadAsync(
        string certRef,
        string keyRef,
        string caRef,
        CancellationToken cancellationToken)
    {
        try
        {
            var certPem = await ReadAsync(certRef, cancellationToken);
            var keyPem = await ReadAsync(keyRef, cancellationToken);
            var caPem = await ReadAsync(caRef, cancellationToken);
            if (string.IsNullOrWhiteSpace(certPem) || string.IsNullOrWhiteSpace(keyPem) || string.IsNullOrWhiteSpace(caPem))
            {
                throw new DockerEngineException(EngineTls.UnreadableMessage);
            }

            return new EngineCertificateMaterial(CreateClient(certPem, keyPem), CreateCa(caPem));
        }
        catch (DockerEngineException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new DockerEngineException(EngineTls.UnreadableMessage);
        }
    }

    private async Task<string?> ReadAsync(string reference, CancellationToken cancellationToken)
    {
        if (!EngineTls.TryReference(reference, out var parsed, out _) || parsed is null)
        {
            return null;
        }

        if (!parsed.Infisical)
        {
            return await File.ReadAllTextAsync(parsed.Location, cancellationToken);
        }

        return await secrets.ReadAsync(parsed.Environment, parsed.Location, cancellationToken);
    }

    internal static X509Certificate2 CreateClient(string certPem, string keyPem)
    {
        using var combined = X509Certificate2.CreateFromPem(certPem, keyPem);
        if (!combined.HasPrivateKey)
        {
            throw new DockerEngineException(EngineTls.UnreadableMessage);
        }

        return X509CertificateLoader.LoadPkcs12(combined.Export(X509ContentType.Pfx), password: null);
    }

    internal static X509Certificate2 CreateCa(string caPem) => X509Certificate2.CreateFromPem(caPem);
}
