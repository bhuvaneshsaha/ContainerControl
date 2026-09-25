using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ContainerControl.Modules.Delivery.Compose;

/// <summary>
/// Docker Compose project name used for Docker Desktop grouping.
/// A top-level compose <c>name:</c> wins. Otherwise the application name is used.
/// </summary>
public static class ComposeProjectName
{
    public const int MaxLength = 63;

    public static string Resolve(string? composeYaml, string? applicationName, Guid applicationId)
    {
        var declared = DeclaredComposeName(composeYaml);
        var sanitized = Sanitize(declared ?? applicationName);
        return sanitized.Length == 0 ? Fallback(applicationId) : sanitized;
    }

    public static string Fallback(Guid applicationId) =>
        "cc-app-" + applicationId.ToString("N")[..8];

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingBreak = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingBreak && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingBreak = false;
                builder.Append(character);
                continue;
            }

            if (character is '-' or '_')
            {
                if (builder.Length > 0)
                {
                    builder.Append(character);
                }

                pendingBreak = false;
                continue;
            }

            pendingBreak = builder.Length > 0;
        }

        var sanitized = TrimSeparators(builder.ToString());
        if (sanitized.Length > MaxLength)
        {
            sanitized = TrimSeparators(sanitized[..MaxLength]);
        }

        return sanitized;
    }

    public static void Stamp(IDictionary<string, string> labels, string project, string service)
    {
        labels["com.docker.compose.project"] = project;
        labels["com.docker.compose.service"] = service;
        labels["com.docker.compose.container-number"] = "1";
        labels["com.docker.compose.oneoff"] = "False";
    }

    /// <summary>
    /// The compose spec <c>name</c> when the file sets a scalar. Null when the file
    /// omits it or cannot be read. An empty string means the key is present but blank.
    /// </summary>
    public static string? DeclaredComposeName(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return null;
        }

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException)
        {
            return null;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return null;
        }

        foreach (var entry in root.Children)
        {
            if (entry.Key is not YamlScalarNode key || !string.Equals(key.Value, "name", StringComparison.Ordinal))
            {
                continue;
            }

            return entry.Value is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;
        }

        return null;
    }

    private static string TrimSeparators(string value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && value[start] is '-' or '_')
        {
            start++;
        }

        while (end >= start && value[end] is '-' or '_')
        {
            end--;
        }

        return start > end ? string.Empty : value[start..(end + 1)];
    }
}
