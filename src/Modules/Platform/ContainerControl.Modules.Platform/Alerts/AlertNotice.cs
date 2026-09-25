using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace ContainerControl.Modules.Platform.Alerts;

public static class AlertKinds
{
    public const string DeployFailed = "deploy.failed";

    public const string ServiceUnhealthy = "service.unhealthy";
}

public sealed record AlertNotice(string Kind, Guid ApplicationId, string ApplicationName, string Message);

public interface IAlertPublisher
{
    Task PublishAsync(AlertNotice notice, CancellationToken cancellationToken);
}

public sealed class SilentAlertPublisher : IAlertPublisher
{
    public static readonly SilentAlertPublisher Instance = new();

    private SilentAlertPublisher()
    {
    }

    public Task PublishAsync(AlertNotice notice, CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class AlertAddress
{
    public static bool TryWebhook(string? value, out string? url, out string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            url = null;
            error = null;
            return true;
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host))
        {
            url = null;
            error = "Enter an http or https webhook URL.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            url = null;
            error = "The webhook URL cannot include a username or password.";
            return false;
        }

        url = uri.AbsoluteUri;
        error = null;
        return true;
    }

    public static bool TryRecipients(string? value, out string? stored, out string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            stored = null;
            error = null;
            return true;
        }

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            stored = null;
            error = null;
            return true;
        }

        if (parts.Any(part => !IsEmail(part)))
        {
            stored = null;
            error = "Enter email recipients separated by commas.";
            return false;
        }

        stored = string.Join(", ", parts);
        error = null;
        return true;
    }

    public static IReadOnlyList<string> SplitRecipients(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }

        return stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static bool MailConfigured(string? host, string? from) =>
        !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(from);

    public static string Json(AlertNotice notice, DateTimeOffset at) =>
        JsonSerializer.Serialize(new
        {
            kind = notice.Kind,
            applicationId = notice.ApplicationId,
            applicationName = notice.ApplicationName,
            message = notice.Message,
            at
        });

    private static bool IsEmail(string value)
    {
        if (value.Length is < 3 or > 200 || value.Contains(' ') || value.Contains('\n') || value.Contains('\r'))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public static class AlertWebhook
{
    public static async Task PostAsync(HttpClient client, string url, string json, CancellationToken cancellationToken)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Alert webhook returned " + (int)response.StatusCode + ".");
        }
    }
}
