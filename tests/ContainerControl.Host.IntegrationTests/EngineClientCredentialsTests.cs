using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ContainerControl.Modules.Platform.Engine;
using Docker.DotNet;
using Microsoft.Net.Http.Client;

namespace ContainerControl.Host.IntegrationTests;

public sealed class EngineClientCredentialsTests
{
    [Fact]
    public async Task A_tcp_client_uses_the_managed_handler_client_certificate_and_ca()
    {
        using var rsa = RSA.Create(2048);
        using var issued = Issue(rsa);
        using var otherKey = RSA.Create(2048);
        using var other = Issue(otherKey);
        var directory = Directory.CreateTempSubdirectory("cc-engine-tls");
        try
        {
            var certPath = Path.Combine(directory.FullName, "client.crt");
            var keyPath = Path.Combine(directory.FullName, "client.key");
            var caPath = Path.Combine(directory.FullName, "ca.crt");
            var certPem = issued.ExportCertificatePem();
            var keyPem = rsa.ExportPkcs8PrivateKeyPem();
            await File.WriteAllTextAsync(certPath, certPem);
            await File.WriteAllTextAsync(keyPath, keyPem);
            await File.WriteAllTextAsync(caPath, certPem);

            var loader = new EngineCertificateLoader(new UnavailableReader());
            using var material = await loader.LoadAsync(
                "file:" + certPath,
                "file:" + keyPath,
                "file:" + caPath,
                CancellationToken.None);
            var credentials = new EngineClientCredentials(material);

            using var docker = new DockerClientConfiguration(new Uri("tcp://127.0.0.1:2376"), credentials)
                .CreateClient(new Version(1, 44), null!);
            var managed = Handler(docker);
            var presented = managed.ClientCertificates.Cast<X509Certificate2>().Single();
            Assert.Equal(material.Client.Thumbprint, presented.Thumbprint);
            Assert.True(presented.HasPrivateKey);
            Assert.NotNull(managed.ServerCertificateValidationCallback);
            using (var trusted = new X509Chain())
            {
                Assert.True(managed.ServerCertificateValidationCallback(null!, material.Client, trusted, SslPolicyErrors.None));
            }

            using (var untrusted = new X509Chain())
            {
                Assert.False(managed.ServerCertificateValidationCallback(
                    null!,
                    other,
                    untrusted,
                    SslPolicyErrors.RemoteCertificateChainErrors));
            }

            using (var missing = new X509Chain())
            {
                Assert.False(managed.ServerCertificateValidationCallback(
                    null!,
                    null,
                    missing,
                    SslPolicyErrors.RemoteCertificateNotAvailable));
            }

            var rejected = Assert.Throws<DockerEngineException>(() => credentials.GetHandler(new SocketsHttpHandler()));
            Assert.Equal(EngineTls.UnreadableMessage, rejected.Message);
            Assert.DoesNotContain(keyPath, rejected.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(keyPem, rejected.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("127.0.0.1", rejected.Message, StringComparison.Ordinal);

            using var serverCertificate = ReloadWithKey(certPem, keyPem);
            await AssertPresentsClientCertificateAsync(credentials, serverCertificate, material.Client.Thumbprint);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task AssertPresentsClientCertificateAsync(
        EngineClientCredentials credentials,
        X509Certificate2 serverCertificate,
        string thumbprint)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var seen = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = HandshakeAsync(listener, serverCertificate, seen, timeout.Token);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var docker = new DockerClientConfiguration(new Uri("tcp://127.0.0.1:" + port), credentials)
            .CreateClient(new Version(1, 44), null!);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => docker.System.GetVersionAsync(timeout.Token));
            var presented = await seen.Task.WaitAsync(timeout.Token);
            Assert.Equal(thumbprint, presented, ignoreCase: true);
        }
        finally
        {
            listener.Stop();
            timeout.Cancel();
            try
            {
                await server.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task HandshakeAsync(
        TcpListener listener,
        X509Certificate2 serverCertificate,
        TaskCompletionSource<string?> seen,
        CancellationToken cancellationToken)
    {
        using var socket = await listener.AcceptSocketAsync(cancellationToken);
        await using var network = new NetworkStream(socket, ownsSocket: true);
        using var ssl = new SslStream(network, leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions
            {
                ServerCertificate = serverCertificate,
                ClientCertificateRequired = true,
                EnabledSslProtocols = SslProtocols.Tls12,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            },
            cancellationToken);
        seen.TrySetResult(ssl.RemoteCertificate?.GetCertHashString());
    }

    private static X509Certificate2 Issue(RSA rsa)
    {
        var request = new CertificateRequest("CN=engine-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
    }

    private static X509Certificate2 ReloadWithKey(string certPem, string keyPem)
    {
        using var combined = X509Certificate2.CreateFromPem(certPem, keyPem);
        return X509CertificateLoader.LoadPkcs12(combined.Export(X509ContentType.Pfx), null);
    }

    private static ManagedHandler Handler(DockerClient docker)
    {
        var http = typeof(DockerClient).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(docker);
        var handler = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(http);
        return Assert.IsType<ManagedHandler>(handler);
    }

    private sealed class UnavailableReader : IEngineSecretReader
    {
        public Task<string?> ReadAsync(string environment, string path, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
