using Microsoft.Extensions.Hosting;

namespace ContainerControl.Modules.Applications.Secrets;

public sealed class SelectingSecretStore : ISecretStore
{
    private readonly InfisicalSecretStore _infisical;
    private readonly DevelopmentFileSecretStore _files;
    private readonly IHostEnvironment _environment;

    public SelectingSecretStore(
        InfisicalSecretStore infisical,
        DevelopmentFileSecretStore files,
        IHostEnvironment environment)
    {
        _infisical = infisical;
        _files = files;
        _environment = environment;
    }

    public Task WriteAsync(SecretAddress address, string value, CancellationToken cancellationToken) =>
        Store(address).WriteAsync(address, value, cancellationToken);

    public Task DeleteAsync(SecretAddress address, CancellationToken cancellationToken) =>
        Store(address).DeleteAsync(address, cancellationToken);

    public Task<string?> ReadAsync(SecretAddress address, CancellationToken cancellationToken) =>
        Store(address).ReadAsync(address, cancellationToken);

    private ISecretStore Store(SecretAddress address)
    {
        if (_infisical.HasCredential(address.Environment))
        {
            return _infisical;
        }

        if (_environment.IsDevelopment())
        {
            return _files;
        }

        throw new SecretStoreException(
            "Infisical machine credentials for this environment are not configured. Create the identity and set the host environment variables. See docs/production-setup.md.");
    }
}
