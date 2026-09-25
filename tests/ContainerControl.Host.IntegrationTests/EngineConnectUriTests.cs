using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;

namespace ContainerControl.Host.IntegrationTests;

public class EngineConnectUriTests
{
    [Theory]
    [InlineData("unix:///var/run/docker.sock", "unix")]
    [InlineData("UNIX:///var/run/docker.sock", "unix")]
    [InlineData("  npipe://./pipe/docker_engine  ", "npipe")]
    [InlineData("Npipe://./pipe/docker_engine", "npipe")]
    [InlineData("tcp://127.0.0.1:2376", "tcp")]
    [InlineData("TCP://10.1.2.3:2376", "tcp")]
    public void Allowed_schemes_parse_with_a_lowercase_scheme(string address, string scheme)
    {
        Assert.True(EngineConnectUri.TryParse(address, out var endpoint, out var error));
        Assert.Null(error);
        Assert.NotNull(endpoint);
        Assert.Equal(scheme, endpoint.Scheme);
    }

    [Theory]
    [InlineData("file:///var/run/docker.sock")]
    [InlineData("/var/run/docker.sock")]
    [InlineData("http://127.0.0.1:2375")]
    [InlineData("HTTP://127.0.0.1:2375")]
    [InlineData("https://127.0.0.1:2376")]
    [InlineData("ssh://git@github.com/repo")]
    public void Disallowed_schemes_are_rejected_with_the_fixed_message(string address)
    {
        Assert.False(EngineConnectUri.TryParse(address, out var endpoint, out var error));
        Assert.Null(endpoint);
        Assert.Equal(EngineConnectUri.SchemeNotAllowedMessage, error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("var/run/docker.sock")]
    [InlineData("leaky-host-9f3a")]
    [InlineData("://leaky-host-9f3a")]
    public void A_missing_scheme_is_rejected_with_the_fixed_message(string? address)
    {
        Assert.False(EngineConnectUri.TryParse(address, out var endpoint, out var error));
        Assert.Null(endpoint);
        Assert.Equal(EngineConnectUri.InvalidUriMessage, error);
        Assert.DoesNotContain("leaky-host-9f3a", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_credential_in_a_rejected_uri_is_not_copied_into_the_error()
    {
        const string address = "ssh://operator:s3cret-token@10.9.8.7:22/tmp/engine";

        Assert.False(EngineConnectUri.TryParse(address, out _, out var error));

        Assert.Equal(EngineConnectUri.SchemeNotAllowedMessage, error);
        Assert.DoesNotContain("s3cret-token", error, StringComparison.Ordinal);
        Assert.DoesNotContain("10.9.8.7", error, StringComparison.Ordinal);
        Assert.DoesNotContain("ssh", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("file:///tmp/engine.sock", EngineConnectUri.SchemeNotAllowedMessage)]
    [InlineData("https://user:s3cret-token@10.9.8.7:2376", EngineConnectUri.SchemeNotAllowedMessage)]
    [InlineData("http://127.0.0.1:2375", EngineConnectUri.SchemeNotAllowedMessage)]
    [InlineData("not-a-uri", EngineConnectUri.InvalidUriMessage)]
    public async Task Connect_rejects_before_opening_a_client(string address, string message)
    {
        var engine = new DockerEngineClient();

        var error = await Assert.ThrowsAsync<DockerEngineException>(() =>
            engine.GetVersionAsync(new DockerEndpoint(address), CancellationToken.None));

        Assert.Equal(message, error.Message);
        Assert.DoesNotContain(address, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret-token", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_rejects_a_disallowed_scheme_before_saving()
    {
        var registry = new HostRegistry(null!, null!, null!, null!, null!);

        var result = await registry.RegisterAsync("local", "file:///tmp/engine.sock", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Null(result.Id);
        Assert.Equal(EngineConnectUri.SchemeNotAllowedMessage, result.Error);
        Assert.DoesNotContain("engine.sock", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_still_asks_for_a_name_and_endpoint()
    {
        var registry = new HostRegistry(null!, null!, null!, null!, null!);

        var result = await registry.RegisterAsync("local", "  ", CancellationToken.None);

        Assert.Equal("Enter a host name and an Engine endpoint.", result.Error);
    }
}
