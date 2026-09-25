using System.Net;
using ContainerControl.Modules.Platform.Alerts;
using ContainerControl.Modules.Runtime.Inspection;

namespace ContainerControl.Host.IntegrationTests;

public class AlertTests
{
    [Fact]
    public void Webhook_address_rejects_credentials_and_non_http_schemes()
    {
        Assert.True(AlertAddress.TryWebhook(" https://hooks.example.com/alerts ", out var url, out var error));
        Assert.Equal("https://hooks.example.com/alerts", url);
        Assert.Null(error);

        Assert.False(AlertAddress.TryWebhook("https://user:secret@hooks.example.com/alerts", out _, out error));
        Assert.Equal("The webhook URL cannot include a username or password.", error);

        Assert.False(AlertAddress.TryWebhook("file:///tmp/alert", out _, out error));
        Assert.Equal("Enter an http or https webhook URL.", error);
    }

    [Fact]
    public void Recipients_are_comma_separated_mailboxes()
    {
        Assert.True(AlertAddress.TryRecipients(" one@example.com, two@example.com ", out var stored, out var error));
        Assert.Equal("one@example.com, two@example.com", stored);
        Assert.Null(error);
        Assert.False(AlertAddress.TryRecipients("not-an-email", out _, out error));
        Assert.Equal("Enter email recipients separated by commas.", error);
    }

    [Fact]
    public void Payload_names_the_event_and_does_not_add_a_secret_field()
    {
        var json = AlertAddress.Json(
            new AlertNotice(AlertKinds.DeployFailed, Guid.Parse("11111111-1111-1111-1111-111111111111"), "billing", "The engine refused the image."),
            new DateTimeOffset(2026, 9, 25, 18, 0, 0, TimeSpan.Zero));

        Assert.Contains("\"kind\":\"deploy.failed\"", json, StringComparison.Ordinal);
        Assert.Contains("billing", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_post_sends_the_json_body()
    {
        string? body = null;
        var handler = new StubHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var client = new HttpClient(handler);

        await AlertWebhook.PostAsync(client, "https://hooks.example.com/alerts", "{\"kind\":\"service.unhealthy\"}", CancellationToken.None);

        Assert.Equal("{\"kind\":\"service.unhealthy\"}", body);
        Assert.Equal("https://hooks.example.com/alerts", handler.LastUri?.ToString());
    }

    [Fact]
    public async Task Webhook_post_reports_an_http_error_without_the_url()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AlertWebhook.PostAsync(client, "https://hooks.example.com/alerts?token=secret", "{}", CancellationToken.None));

        Assert.Equal("Alert webhook returned 502.", error.Message);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mail_is_configured_only_when_host_and_from_are_set()
    {
        Assert.False(AlertAddress.MailConfigured("", "ops@example.com"));
        Assert.False(AlertAddress.MailConfigured("smtp.example.com", " "));
        Assert.True(AlertAddress.MailConfigured("smtp.example.com", "ops@example.com"));
    }

    [Fact]
    public void Unhealthy_alerts_fire_on_the_transition_only()
    {
        Assert.True(HealthAlertGate.ShouldAlert(null, "unhealthy"));
        Assert.True(HealthAlertGate.ShouldAlert("healthy", "unhealthy"));
        Assert.False(HealthAlertGate.ShouldAlert("unhealthy", "unhealthy"));
        Assert.False(HealthAlertGate.ShouldAlert("starting", "healthy"));
        Assert.False(HealthAlertGate.ShouldAlert(null, null));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }
}
