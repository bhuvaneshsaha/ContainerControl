namespace ContainerControl.Modules.Registries.Connections;

public sealed class RegistryConnection
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Server { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string UsernamePath { get; set; } = string.Empty;

    public string PasswordPath { get; set; } = string.Empty;

    public string? AccessKeyPath { get; set; }

    public string? SecretKeyPath { get; set; }

    public DateTimeOffset? EcrTokenExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public static class RegistryKinds
{
    public static readonly string[] All = ["Acr", "Ecr", "DockerHub", "Harbor"];

    public static bool TryNormalize(string? kind, out string normalized)
    {
        normalized = All.FirstOrDefault(item => string.Equals(item, kind?.Trim(), StringComparison.Ordinal)) ?? string.Empty;
        return normalized.Length > 0;
    }
}

public static class RegistryImage
{
    public static string HostOf(string image)
    {
        var name = image.Split('@')[0];
        var slash = name.IndexOf('/');
        if (slash < 0)
        {
            return "docker.io";
        }

        var first = name[..slash];
        if (first.Contains('.') || first.Contains(':') || first.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return first;
        }

        return "docker.io";
    }

    public static bool Matches(string image, string server, string kind)
    {
        var host = HostOf(image);
        var wanted = server.Trim();
        if (kind == "DockerHub" && IsDockerHub(host))
        {
            return IsDockerHub(wanted);
        }

        return host.Equals(wanted, StringComparison.OrdinalIgnoreCase);
    }

    public static string? EcrRegion(string server)
    {
        const string marker = ".dkr.ecr.";
        var index = server.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var rest = server[(index + marker.Length)..];
        var end = rest.IndexOf('.');
        return end > 0 ? rest[..end] : null;
    }

    public static bool IsDue(DateTimeOffset? expiresAtUtc, DateTimeOffset now) =>
        expiresAtUtc is null || expiresAtUtc <= now.AddHours(2);

    private static bool IsDockerHub(string host) =>
        host.Equals("docker.io", StringComparison.OrdinalIgnoreCase)
        || host.Equals("index.docker.io", StringComparison.OrdinalIgnoreCase)
        || host.Equals("registry-1.docker.io", StringComparison.OrdinalIgnoreCase);
}
