using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Platform.Engine;

namespace ContainerControl.Host.Hosting;

/// <summary>
/// Reads Engine client material from the same secret store as application secrets.
/// The host row keeps the reference. This reader returns the value only in memory.
/// </summary>
public sealed class InfisicalEngineSecretReader(ISecretStore secrets) : IEngineSecretReader
{
    public async Task<string?> ReadAsync(string environment, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await secrets.ReadAsync(new SecretAddress(environment, path), cancellationToken);
        }
        catch (SecretStoreException)
        {
            return null;
        }
    }
}
