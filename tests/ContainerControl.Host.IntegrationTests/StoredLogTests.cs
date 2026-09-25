using ContainerControl.Modules.Runtime.Inspection;

namespace ContainerControl.Host.IntegrationTests;

public class StoredLogTests
{
    [Fact]
    public void Redaction_removes_secret_values_and_keeps_the_rest_of_the_line()
    {
        var line = LogRedactor.Apply("login ok password=s3cr3t-value token=abc", ["s3cr3t-value", "ab"]);

        Assert.Equal("login ok password=[redacted] token=abc", line);
        Assert.DoesNotContain("s3cr3t-value", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_keeps_lines_after_the_cursor_and_drops_older_ones()
    {
        var raw = """
            2026-09-25T18:00:00.000000000Z old
            2026-09-25T18:00:01.100000000Z kept
            not-a-timestamp
            """;
        var cursor = DateTimeOffset.Parse("2026-09-25T18:00:00Z");

        var lines = TimestampedLog.Parse(raw, cursor);

        Assert.Equal(["kept"], lines.Select(line => line.Text).ToArray());
        Assert.True(lines[0].At > cursor);
    }

    [Fact]
    public void Retention_cutoff_is_at_least_one_day()
    {
        var now = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(now.AddDays(-14), StoredLogText.Cutoff(now, 14));
        Assert.Equal(now.AddDays(-1), StoredLogText.Cutoff(now, 0));
    }

    [Fact]
    public void The_same_stored_line_hashes_to_the_same_value()
    {
        Assert.Equal(StoredLogText.Hash("hello"), StoredLogText.Hash("hello"));
        Assert.NotEqual(StoredLogText.Hash("hello"), StoredLogText.Hash("hello "));
    }
}
