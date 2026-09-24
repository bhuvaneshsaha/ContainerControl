using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Edge.Domains;

namespace ContainerControl.Host.IntegrationTests;

public sealed class DeployRequestTests
{
    private const string NginxCompose = """
        services:
          web:
            image: nginx:stable
            x-containercontrol:
              exposed: true
              port: 80
        """;

    [Fact]
    public void Nginx_welcome_compose_is_accepted()
    {
        var plan = ComposePolicy.Parse(NginxCompose);

        Assert.True(plan.Accepted);
        var service = Assert.Single(plan.Services);
        Assert.Equal("web", service.Name);
        Assert.Equal("nginx:stable", service.Image);
        Assert.True(service.Exposed);
        Assert.Equal(80, service.Port);
    }

    [Theory]
    [InlineData("http://nginx.apps.example.com/", "apps.example.com")]
    [InlineData("http://nginx.apps.example.com", "apps.example.com")]
    [InlineData("https://nginx.apps.example.com/welcome", "https://apps.example.com")]
    [InlineData("nginx.apps.example.com:80", "apps.example.com.")]
    [InlineData("NGINX.Apps.Example.COM", "Apps.Example.com")]
    public void Pasted_url_is_under_the_allowed_domain(string hostname, string domain)
    {
        Assert.Equal("nginx.apps.example.com", PublicHostname.Normalize(hostname));
        Assert.True(PublicHostname.IsUnder(hostname, domain));
    }

    [Theory]
    [InlineData("http://evilapps.example.com/", "apps.example.com")]
    [InlineData("notexample.com", "example.com")]
    [InlineData("http://nginx.other.example/", "apps.example.com")]
    [InlineData(null, "apps.example.com")]
    [InlineData("http://nginx.apps.example.com/", "*.apps.example.com")]
    public void Unrelated_names_are_rejected(string? hostname, string domain)
    {
        Assert.False(PublicHostname.IsUnder(hostname, domain));
    }
}
