namespace ContainerControl.Modules.Edge.Domains;

/// <summary>
/// Public route names. A pasted URL is reduced to the host before it is compared
/// with an allowed domain or written into a Traefik rule.
/// </summary>
public static class PublicHostname
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var absolute)
                || (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrEmpty(absolute.Host))
            {
                return null;
            }

            text = absolute.IdnHost;
        }
        else
        {
            var slash = text.IndexOf('/');
            if (slash >= 0)
            {
                text = text[..slash];
            }

            var colon = text.LastIndexOf(':');
            if (colon > 0 && text.IndexOf(':') == colon && text[(colon + 1)..].All(char.IsDigit))
            {
                text = text[..colon];
            }
        }

        text = text.Trim().Trim('.').ToLowerInvariant();
        if (text.Length == 0 || text.Contains(' ') || text.Contains('/') || text.Contains('*') || text.Contains(':'))
        {
            return null;
        }

        return text;
    }

    public static bool IsUnder(string? hostname, string? domain)
    {
        var host = Normalize(hostname);
        var suffix = Normalize(domain);
        if (host is null || suffix is null)
        {
            return false;
        }

        return host.Equals(suffix, StringComparison.Ordinal)
            || host.EndsWith("." + suffix, StringComparison.Ordinal);
    }
}
