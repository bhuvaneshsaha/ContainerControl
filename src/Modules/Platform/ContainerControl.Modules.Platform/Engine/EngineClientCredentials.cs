using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Docker.DotNet;
using Microsoft.Net.Http.Client;

namespace ContainerControl.Modules.Platform.Engine;

internal sealed class EngineClientCredentials : Credentials
{
    private readonly EngineCertificateMaterial _material;

    public EngineClientCredentials(EngineCertificateMaterial material)
    {
        _material = material;
    }

    public override void Dispose()
    {
    }

    public override bool IsTlsCredentials() => true;

    public override HttpMessageHandler GetHandler(HttpMessageHandler handler)
    {
        var ca = _material.Ca;
        if (handler is ManagedHandler managed)
        {
            managed.ClientCertificates ??= new X509Certificate2Collection();
            if (!managed.ClientCertificates.Contains(_material.Client))
            {
                managed.ClientCertificates.Add(_material.Client);
            }

            managed.ServerCertificateValidationCallback = (_, certificate, chain, _) =>
                Trusted(certificate, chain, ca);
            return managed;
        }

        if (handler is HttpClientHandler http)
        {
            http.ClientCertificates.Add(_material.Client);
            http.ServerCertificateCustomValidationCallback = (_, certificate, chain, _) =>
                Trusted(certificate, chain, ca);
            return http;
        }

        throw new DockerEngineException(EngineTls.UnreadableMessage);
    }

    private static bool Trusted(X509Certificate? certificate, X509Chain? chain, X509Certificate2 ca)
    {
        if (certificate is null || chain is null)
        {
            return false;
        }

        X509Certificate2? owned = null;
        try
        {
            var leaf = certificate as X509Certificate2;
            if (leaf is null)
            {
                owned = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                leaf = owned;
            }

            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Clear();
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(leaf);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            owned?.Dispose();
        }
    }
}
