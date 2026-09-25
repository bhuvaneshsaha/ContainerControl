using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ContainerControl.Modules.Platform.Engine;

namespace ContainerControl.Host.IntegrationTests;

public sealed class EngineCertificateLoaderTests
{
    [Fact]
    public async Task File_references_load_a_client_key_and_ca_without_storing_pem()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=engine-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var issued = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        var directory = Directory.CreateTempSubdirectory("cc-engine-tls");
        try
        {
            var certPath = Path.Combine(directory.FullName, "client.crt");
            var keyPath = Path.Combine(directory.FullName, "client.key");
            var caPath = Path.Combine(directory.FullName, "ca.crt");
            await File.WriteAllTextAsync(certPath, issued.ExportCertificatePem());
            await File.WriteAllTextAsync(keyPath, rsa.ExportPkcs8PrivateKeyPem());
            await File.WriteAllTextAsync(caPath, issued.ExportCertificatePem());

            var loader = new EngineCertificateLoader(new UnavailableReader());
            using var material = await loader.LoadAsync("file:" + certPath, "file:" + keyPath, "file:" + caPath, CancellationToken.None);

            Assert.True(material.Client.HasPrivateKey);
            Assert.False(string.IsNullOrWhiteSpace(material.Ca.Thumbprint));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_missing_file_is_a_fixed_message()
    {
        var loader = new EngineCertificateLoader(new UnavailableReader());

        var error = await Assert.ThrowsAsync<DockerEngineException>(() =>
            loader.LoadAsync("file:/tmp/missing-client.crt", "file:/tmp/missing-client.key", "file:/tmp/missing-ca.crt", CancellationToken.None));

        Assert.Equal(EngineTls.UnreadableMessage, error.Message);
        Assert.DoesNotContain("missing-client", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_relative_file_reference_is_rejected()
    {
        Assert.False(EngineTls.TryReference("file:certs/client.crt", out var reference, out var error));
        Assert.Null(reference);
        Assert.Equal(EngineTls.MaterialMessage, error);
    }

    [Fact]
    public void An_infisical_reference_keeps_the_environment_and_path()
    {
        Assert.True(EngineTls.TryReference("infisical:prod:/engine/client-cert", out var reference, out var error));
        Assert.Null(error);
        Assert.NotNull(reference);
        Assert.True(reference.Infisical);
        Assert.Equal("prod", reference.Environment);
        Assert.Equal("/engine/client-cert", reference.Location);
        Assert.Equal("infisical:prod:/engine/client-cert", reference.Stored);
        Assert.DoesNotContain("-----BEGIN", reference.Stored, StringComparison.Ordinal);
    }

    private sealed class UnavailableReader : IEngineSecretReader
    {
        public Task<string?> ReadAsync(string environment, string path, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
