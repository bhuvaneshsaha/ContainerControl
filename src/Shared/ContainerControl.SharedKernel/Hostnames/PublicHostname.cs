using System.Globalization;

namespace ContainerControl.SharedKernel.Hostnames;

/// <summary>
/// Public route names. A pasted URL is reduced to one DNS hostname before it is
/// compared with an allowed domain or written into a Traefik <c>Host(`…`)</c> rule.
/// Letters, digits, hyphens, and dots are the only characters that survive, so the
/// name cannot close the backtick string or add another matcher.
/// </summary>
public static class PublicHostname
{
    public const string InvalidMessage = "Enter a DNS hostname. Wildcards and routing characters are not allowed.";

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
        if (text.Length == 0)
        {
            return null;
        }

        try
        {
            text = new IdnMapping { UseStd3AsciiRules = true }.GetAscii(text);
        }
        catch (ArgumentException)
        {
            return null;
        }

        text = text.ToLowerInvariant();
        return IsDnsHostname(text) ? text : null;
    }

    /// <summary>
    /// Empty input is allowed. Any other value must be one DNS hostname.
    /// </summary>
    public static bool TryCanonical(string? value, out string? hostname, out string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            hostname = null;
            error = null;
            return true;
        }

        hostname = Normalize(value);
        if (hostname is null)
        {
            error = InvalidMessage;
            return false;
        }

        error = null;
        return true;
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

    /// <summary>
    /// Builds a single <c>Host(`name`)</c> matcher, or returns null when the name
    /// is not a DNS hostname. Callers must not interpolate the raw value.
    /// </summary>
    public static string? TraefikHostRule(string? hostname)
    {
        var host = Normalize(hostname);
        if (host is null || !IsDnsHostname(host) || !IsSafeRuleLiteral(host))
        {
            return null;
        }

        return "Host(`" + host + "`)";
    }

    private static bool IsSafeRuleLiteral(string host) =>
        host.IndexOfAny(['`', '(', ')', '{', '}', '|', '&', '!', '\\', '"', '\r', '\n']) < 0;

    private static bool IsDnsHostname(string text)
    {
        if (text.Length is 0 or > 253)
        {
            return false;
        }

        var labels = text.Split('.');
        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-')
            {
                return false;
            }

            foreach (var ch in label)
            {
                if (ch is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
