using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ContainerControl.Modules.Applications.Templates;

/// <summary>
/// Finds secret material in a template. Messages name the problem and never repeat the value.
/// </summary>
public static partial class TemplateComposeSecrets
{
    public const string SecretValueMessage =
        "The template contains a secret value. Remove it and assign the secret on the application.";

    public const string PrivateKeyMessage =
        "The template contains a private key. Remove it and assign the secret on the application.";

    public const string EnvFileMessage =
        "The template cannot use env_file. Assign secrets on the application.";

    public const string ComposeSecretsMessage =
        "The template cannot declare compose secrets. Assign secrets on the application.";

    private static readonly string[] SecretMarkers =
    [
        "password", "passwd", "pwd", "secret", "token", "apikey", "privatekey",
        "clientsecret", "connectionstring", "credential"
    ];

    public static IReadOnlyList<string> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var messages = new List<string>();
        if (text.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            messages.Add(PrivateKeyMessage);
        }

        InspectScalar(null, text, messages);
        return messages;
    }

    public static IReadOnlyList<string> FindInCompose(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return [];
        }

        var messages = Find(yaml).ToList();
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException)
        {
            return messages;
        }

        foreach (var document in stream.Documents)
        {
            Walk(document.RootNode, null, messages);
        }

        return messages;
    }

    private static void Walk(YamlNode node, string? key, List<string> messages)
    {
        switch (node)
        {
            case YamlMappingNode map:
                foreach (var entry in map.Children)
                {
                    var childKey = entry.Key is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;
                    if (childKey.Equals("env_file", StringComparison.Ordinal))
                    {
                        Add(messages, EnvFileMessage);
                    }

                    if (childKey.Equals("secrets", StringComparison.Ordinal))
                    {
                        Add(messages, ComposeSecretsMessage);
                    }

                    Walk(entry.Value, childKey, messages);
                }

                break;
            case YamlSequenceNode sequence:
                foreach (var child in sequence.Children)
                {
                    Walk(child, key, messages);
                }

                break;
            case YamlScalarNode scalar:
                InspectScalar(key, scalar.Value ?? string.Empty, messages);
                if (key is not null
                    && key.Equals("environment", StringComparison.OrdinalIgnoreCase)
                    && (scalar.Value ?? string.Empty).Contains('='))
                {
                    var split = scalar.Value!.IndexOf('=');
                    var envKey = scalar.Value[..split];
                    var envValue = scalar.Value[(split + 1)..];
                    if (IsSecretKey(envKey) && !IsPlaceholder(envValue))
                    {
                        Add(messages, SecretValueMessage);
                    }
                }

                break;
        }
    }

    private static void InspectScalar(string? key, string value, List<string> messages)
    {
        if (key is not null && IsSecretKey(key) && !IsPlaceholder(value))
        {
            Add(messages, SecretValueMessage);
        }

        if (HasInlineSecret(value) || HasUrlPassword(value))
        {
            Add(messages, SecretValueMessage);
        }
    }

    private static bool IsSecretKey(string key)
    {
        var compact = SecretKeyPattern().Replace(key, string.Empty).ToLowerInvariant();
        return SecretMarkers.Any(marker => compact.Contains(marker, StringComparison.Ordinal));
    }

    private static bool IsPlaceholder(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        return PlaceholderPattern().IsMatch(trimmed);
    }

    private static bool HasInlineSecret(string value)
    {
        foreach (Match match in InlineSecretPattern().Matches(value))
        {
            if (!IsPlaceholder(match.Groups["value"].Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUrlPassword(string value)
    {
        foreach (Match match in UrlPasswordPattern().Matches(value))
        {
            var password = match.Groups["pass"].Value;
            if (!IsPlaceholder(password) && !password.Contains("${", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void Add(List<string> messages, string message)
    {
        if (!messages.Contains(message, StringComparer.Ordinal))
        {
            messages.Add(message);
        }
    }

    [GeneratedRegex("[^A-Za-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex SecretKeyPattern();

    [GeneratedRegex(@"^\$(\{[A-Za-z_][A-Za-z0-9_]*\}|[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(
        @"(?i)(?:^|[^A-Za-z0-9])(?:[A-Za-z0-9]+_)*(?:password|passwd|pwd|secret|token|api[-_]?key|private[-_]?key|client[-_]?secret)\s*[:=]\s*(?<value>\S+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex InlineSecretPattern();

    [GeneratedRegex(
        @"(?i)[a-z][a-z0-9+.-]*://[^/\s:@]+:(?<pass>[^/\s@]+)@",
        RegexOptions.CultureInvariant)]
    private static partial Regex UrlPasswordPattern();
}
