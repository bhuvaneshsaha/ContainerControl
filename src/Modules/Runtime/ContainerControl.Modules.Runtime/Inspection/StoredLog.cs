using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ContainerControl.Modules.Runtime.Inspection;

public sealed record ParsedLogLine(DateTimeOffset At, string Text);

public static class LogRedactor
{
    public const string Mask = "[redacted]";

    public static string Apply(string text, IEnumerable<string> secretValues)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var secrets = secretValues
            .Where(value => !string.IsNullOrEmpty(value) && value.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
        foreach (var secret in secrets)
        {
            text = text.Replace(secret, Mask, StringComparison.Ordinal);
        }

        return text;
    }
}

public static class TimestampedLog
{
    public static IReadOnlyList<ParsedLogLine> Parse(string raw, DateTimeOffset? after)
    {
        var lines = new List<ParsedLogLine>();
        if (string.IsNullOrEmpty(raw))
        {
            return lines;
        }

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || !TrySplit(line, out var at, out var text))
            {
                continue;
            }

            if (after is not null && at <= after.Value)
            {
                continue;
            }

            lines.Add(new ParsedLogLine(at, text));
        }

        return lines;
    }

    public static bool TrySplit(string line, out DateTimeOffset at, out string text)
    {
        at = default;
        text = string.Empty;
        var space = line.IndexOf(' ');
        if (space <= 0)
        {
            return false;
        }

        var stamp = line[..space];
        if (!stamp.EndsWith('Z') || !DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at))
        {
            return false;
        }

        text = line[(space + 1)..];
        return true;
    }
}

public static class StoredLogText
{
    public const int MaxLength = 2000;

    public static string Clip(string text) =>
        text.Length <= MaxLength ? text : text[..MaxLength];

    public static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public static DateTimeOffset Cutoff(DateTimeOffset now, int retentionDays)
    {
        var days = retentionDays < 1 ? 1 : retentionDays;
        return now.AddDays(-days);
    }
}

public sealed class StoredLogLine
{
    public long Id { get; set; }

    public Guid ApplicationId { get; set; }

    public string ContainerId { get; set; } = string.Empty;

    public string Service { get; set; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; set; }

    public string Text { get; set; } = string.Empty;

    public string TextHash { get; set; } = string.Empty;
}

public sealed class LogCursor
{
    public string ContainerId { get; set; } = string.Empty;

    public DateTimeOffset LastAtUtc { get; set; }
}
