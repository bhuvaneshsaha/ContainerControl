using ContainerControl.Modules.Platform.Persistence;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Platform.Alerts;

public sealed class AlertSetting
{
    public int Id { get; set; }

    public string? WebhookUrl { get; set; }

    public string? Recipients { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed record AlertSettingsView(string? WebhookUrl, string? Recipients, bool SmtpConfigured);

public sealed class AlertAdmin
{
    private readonly PlatformDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public AlertAdmin(PlatformDbContext db, IConfiguration configuration, IClock clock)
    {
        _db = db;
        _configuration = configuration;
        _clock = clock;
    }

    public async Task<AlertSettingsView> GetAsync(CancellationToken cancellationToken)
    {
        var row = await _db.AlertSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        return new AlertSettingsView(row?.WebhookUrl, row?.Recipients, SmtpConfigured());
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(string? webhookUrl, string? recipients, CancellationToken cancellationToken)
    {
        if (!AlertAddress.TryWebhook(webhookUrl, out var url, out var webhookError))
        {
            return (false, webhookError);
        }

        if (!AlertAddress.TryRecipients(recipients, out var storedRecipients, out var recipientError))
        {
            return (false, recipientError);
        }

        var row = await _db.AlertSettings.SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (row is null)
        {
            row = new AlertSetting { Id = 1 };
            _db.AlertSettings.Add(row);
        }

        row.WebhookUrl = url;
        row.Recipients = storedRecipients;
        row.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private bool SmtpConfigured() =>
        AlertAddress.MailConfigured(_configuration["Alerts:Smtp:Host"], _configuration["Alerts:Smtp:From"]);
}

public sealed class AlertPublisher : IAlertPublisher
{
    public const string HttpClientName = "containercontrol-alerts";

    private readonly PlatformDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClock _clock;
    private readonly ILogger<AlertPublisher> _logger;

    public AlertPublisher(
        PlatformDbContext db,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IClock clock,
        ILogger<AlertPublisher> logger)
    {
        _db = db;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task PublishAsync(AlertNotice notice, CancellationToken cancellationToken)
    {
        var row = await _db.AlertSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        var webhook = row?.WebhookUrl;
        var recipients = AlertAddress.SplitRecipients(row?.Recipients);
        if (string.IsNullOrWhiteSpace(webhook) && recipients.Count == 0)
        {
            return;
        }

        var json = AlertAddress.Json(notice, _clock.UtcNow);
        if (!string.IsNullOrWhiteSpace(webhook))
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                await AlertWebhook.PostAsync(client, webhook, json, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "Alert webhook was not delivered for {AlertKind}. {ExceptionType}",
                    notice.Kind,
                    exception.GetType().Name);
            }
        }

        if (recipients.Count == 0)
        {
            return;
        }

        var host = _configuration["Alerts:Smtp:Host"];
        var from = _configuration["Alerts:Smtp:From"];
        if (!AlertAddress.MailConfigured(host, from))
        {
            _logger.LogWarning("Alert email was not sent because SMTP is not configured.");
            return;
        }

        try
        {
            await SendMailAsync(host!, from!, recipients, notice, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Alert email was not sent for {AlertKind}. {ExceptionType}",
                notice.Kind,
                exception.GetType().Name);
        }
    }

    private async Task SendMailAsync(
        string host,
        string from,
        IReadOnlyList<string> recipients,
        AlertNotice notice,
        CancellationToken cancellationToken)
    {
        var port = 25;
        var configuredPort = _configuration["Alerts:Smtp:Port"];
        if (int.TryParse(configuredPort, out var parsed) && parsed is > 0 and <= 65535)
        {
            port = parsed;
        }

        using var message = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(from),
            Subject = Subject(notice),
            Body = notice.ApplicationName + "\n" + notice.Message
        };
        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        using var client = new System.Net.Mail.SmtpClient(host, port);
        var username = _configuration["Alerts:Smtp:Username"];
        var password = _configuration["Alerts:Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new System.Net.NetworkCredential(username, password ?? string.Empty);
        }

        client.EnableSsl = string.Equals(_configuration["Alerts:Smtp:EnableSsl"], "true", StringComparison.OrdinalIgnoreCase);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }

    private static string Subject(AlertNotice notice) =>
        notice.Kind switch
        {
            AlertKinds.DeployFailed => "ContainerControl deploy failed for " + notice.ApplicationName,
            AlertKinds.ServiceUnhealthy => "ContainerControl service unhealthy for " + notice.ApplicationName,
            _ => "ContainerControl alert for " + notice.ApplicationName
        };
}
