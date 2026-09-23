namespace ContainerControl.Modules.Applications.Secrets;

public sealed class SecretReference
{
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    public string Environment { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string InjectionMode { get; set; } = "env";

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed record SecretSnapshot(string Name, string Path, string InjectionMode);

public sealed record SecretAddress(string Environment, string Path);

public interface ISecretCatalog
{
    Task<IReadOnlyList<SecretSnapshot>> ListAsync(Guid teamId, string environment, CancellationToken cancellationToken);
}

public interface ISecretStore
{
    Task WriteAsync(SecretAddress address, string value, CancellationToken cancellationToken);

    Task DeleteAsync(SecretAddress address, CancellationToken cancellationToken);

    Task<string?> ReadAsync(SecretAddress address, CancellationToken cancellationToken);
}
