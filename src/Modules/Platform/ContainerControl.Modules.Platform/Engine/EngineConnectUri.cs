namespace ContainerControl.Modules.Platform.Engine;

/// <summary>
/// The only Engine connect-URI check. Schemes are compared without regard to case.
/// The error text never includes the supplied address.
/// </summary>
public static class EngineConnectUri
{
    public const string InvalidUriMessage =
        "The Engine endpoint must be an absolute URI, such as unix:///var/run/docker.sock.";

    public const string SchemeNotAllowedMessage =
        "The Engine endpoint scheme is not allowed. Use unix, npipe, or tcp.";

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "unix",
        "npipe",
        "tcp"
    };

    public static bool TryParse(string? address, out Uri? endpoint, out string? error)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(address)
            || !Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Scheme))
        {
            error = InvalidUriMessage;
            return false;
        }

        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            error = SchemeNotAllowedMessage;
            return false;
        }

        endpoint = uri;
        error = null;
        return true;
    }

    public static Uri Parse(string? address)
    {
        if (!TryParse(address, out var endpoint, out var error) || endpoint is null)
        {
            throw new DockerEngineException(error ?? SchemeNotAllowedMessage);
        }

        return endpoint;
    }
}
