namespace ContainerControl.Modules.Platform.Engine;

/// <summary>
/// References for a tcp Engine client certificate, key, and CA.
/// The database stores the reference. It does not store PEM.
/// </summary>
public static class EngineTls
{
    public const string CleartextMessage =
        "A tcp Engine endpoint requires a client certificate, key, and CA reference.";

    public const string UnreadableMessage =
        "The Engine client certificate, key, or CA reference could not be read.";

    public const string MaterialMessage =
        "Engine certificate references must be an Infisical path or a file path.";

    public static bool IsOperatorMessage(string? message) =>
        message == CleartextMessage || message == UnreadableMessage || message == MaterialMessage;

    public static bool TryReference(string? raw, out EngineTlsReference? reference, out string? error)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = null;
            return true;
        }

        var value = raw.Trim();
        if (value.Length > 500
            || value.Contains("-----BEGIN", StringComparison.Ordinal)
            || value.Contains('\n')
            || value.Contains('\r'))
        {
            error = MaterialMessage;
            return false;
        }

        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var path = value["file:".Length..];
            if (!Path.IsPathRooted(path))
            {
                error = MaterialMessage;
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                error = MaterialMessage;
                return false;
            }
            reference = new EngineTlsReference(Stored: "file:" + full, Infisical: false, Environment: "", Location: full);
            error = null;
            return true;
        }

        if (value.StartsWith("infisical:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = value["infisical:".Length..];
            var split = rest.IndexOf(':');
            if (split <= 0 || split == rest.Length - 1)
            {
                error = MaterialMessage;
                return false;
            }

            var environment = rest[..split];
            var path = rest[(split + 1)..].Trim();
            if (environment is not ("dev" or "staging" or "prod")
                || path.Length == 0
                || path.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                error = MaterialMessage;
                return false;
            }

            reference = new EngineTlsReference(
                Stored: "infisical:" + environment + ":" + path,
                Infisical: true,
                Environment: environment,
                Location: path);
            error = null;
            return true;
        }

        error = MaterialMessage;
        return false;
    }
}

public sealed record EngineTlsReference(string Stored, bool Infisical, string Environment, string Location);
