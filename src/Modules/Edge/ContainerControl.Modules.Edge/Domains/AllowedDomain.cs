namespace ContainerControl.Modules.Edge.Domains;

public sealed class AllowedDomain
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public interface IEdgeGateway
{
    string EdgeNetworkName { get; }

    bool UseTls => false;

    Task<bool> HostnameAllowedAsync(string? hostname, CancellationToken cancellationToken);

    Task EnsureEdgeAsync(string dockerEndpoint, CancellationToken cancellationToken);

    IReadOnlyDictionary<string, string> LabelsFor(string routerName, string hostname, int port);
}
