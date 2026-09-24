using Microsoft.Extensions.Hosting;

namespace ContainerControl.Modules.Applications.Secrets;

/// <summary>
/// Development stand-in used only when Infisical credentials are absent.
/// Values stay in a local file outside PostgreSQL and are never returned by the API.
/// </summary>
public sealed class DevelopmentFileSecretStore : ISecretStore
{
    private readonly string _root;

    public DevelopmentFileSecretStore(IHostEnvironment environment)
    {
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "deploy", "local", "secret-store"));
    }

    public Task WriteAsync(SecretAddress address, string value, CancellationToken cancellationToken)
    {
        var path = FileFor(address);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, value, cancellationToken);
    }

    public Task DeleteAsync(SecretAddress address, CancellationToken cancellationToken)
    {
        var path = FileFor(address);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task<string?> ReadAsync(SecretAddress address, CancellationToken cancellationToken)
    {
        var path = FileFor(address);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private string FileFor(SecretAddress address)
    {
        if (address.Environment is not ("dev" or "staging" or "prod"))
        {
            throw new SecretStoreException("The secret environment is not recognized.");
        }

        var name = address.Path.Trim().Trim('/');
        foreach (var character in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(character, '_');
        }

        name = name.Replace('/', '_').Replace('\\', '_');
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SecretStoreException("The secret path is empty.");
        }

        var directory = Path.GetFullPath(Path.Combine(_root, address.Environment));
        var file = Path.GetFullPath(Path.Combine(directory, name + ".secret"));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!file.StartsWith(_root + Path.DirectorySeparatorChar, comparison)
            && !string.Equals(file, _root, comparison))
        {
            throw new SecretStoreException("The secret path is not allowed.");
        }

        return file;
    }
}
