using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Docker.DotNet;

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
        if (handler is not HttpClientHandler http)
        {
            throw new DockerEngineException(EngineTls.UnreadableMessage);
        }

        http.ClientCertificates.Add(_material.Client);
        var ca = _material.Ca;
        http.ServerCertificateCustomValidationCallback = (_, certificate, chain, _) =>
        {
            if (certificate is null || chain is null)
            {
                return false;
            }

            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Clear();
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(certificate);
        };
        return http;
    }
}
