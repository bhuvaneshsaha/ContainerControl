using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Edge.Domains;
using ContainerControl.SharedKernel.Hostnames;

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
    [InlineData("http://user:pass@nginx.apps.example.com/a?b=1&c=2", "apps.example.com")]
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

    [Theory]
    [InlineData("my-app.apps.example.com", "apps.example.com")]
    [InlineData("a.b.apps.example.com", "apps.example.com")]
    [InlineData("web.apps.localhost", "apps.localhost")]
    public void Dns_hostnames_normalize_and_stay_under_the_domain(string hostname, string domain)
    {
        Assert.Equal(hostname, PublicHostname.Normalize(hostname));
        Assert.True(PublicHostname.IsUnder(hostname, domain));
        Assert.Equal("Host(`" + hostname + "`)", PublicHostname.TraefikHostRule(hostname));
        Assert.True(PublicHostname.TryCanonical(hostname, out var canonical, out var error));
        Assert.Equal(hostname, canonical);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("evil.com`)||Host(`web.apps.example.com")]
    [InlineData("web.apps.example.com`)||Host(`evil.com")]
    [InlineData("a.apps.example.com`)||Host(`evil.com`)||Host(`b.apps.example.com")]
    [InlineData("web.apps.example.com`) || Host(`evil.com")]
    [InlineData("`web.apps.example.com`")]
    [InlineData("(web.apps.example.com)")]
    [InlineData("web.apps.example.com)")]
    [InlineData("web.apps.example.com`")]
    [InlineData("app_name.example.com")]
    [InlineData("-app.example.com")]
    [InlineData("app-.example.com")]
    [InlineData("app..example.com")]
    [InlineData("*.apps.example.com")]
    [InlineData("app.example.com evil.com")]
    [InlineData("app.example.com\nHost(`evil.com`)")]
    public void Rule_injection_and_non_dns_names_are_rejected(string hostname)
    {
        Assert.Null(PublicHostname.Normalize(hostname));
        Assert.False(PublicHostname.IsUnder(hostname, "apps.example.com"));
        Assert.False(PublicHostname.IsUnder(hostname, "example.com"));
        Assert.Null(PublicHostname.TraefikHostRule(hostname));
        Assert.False(PublicHostname.TryCanonical(hostname, out var canonical, out var error));
        Assert.Null(canonical);
        Assert.Equal(PublicHostname.InvalidMessage, error);
    }

    [Fact]
    public void Empty_hostname_is_allowed_on_write()
    {
        Assert.True(PublicHostname.TryCanonical(null, out var hostname, out var error));
        Assert.Null(hostname);
        Assert.Null(error);
        Assert.True(PublicHostname.TryCanonical("   ", out hostname, out error));
        Assert.Null(hostname);
        Assert.Null(error);
    }

    [Fact]
    public void Host_label_is_one_dns_matcher()
    {
        var gateway = new EdgeGateway(null!, null!, null!, null!);
        var labels = gateway.LabelsFor("web", "https://My-App.apps.example.com/welcome", 80);
        var rule = labels["traefik.http.routers.web.rule"];

        Assert.Equal("Host(`my-app.apps.example.com`)", rule);
        Assert.Equal("web", labels["traefik.http.routers.web.entrypoints"]);
        Assert.False(labels.ContainsKey("traefik.http.routers.web.tls"));
        Assert.False(labels.ContainsKey("traefik.http.routers.web.tls.certresolver"));
        Assert.DoesNotContain("||", rule);
        Assert.Equal(2, rule.Count(character => character == '`'));
        Assert.DoesNotContain("(", rule[5..^1]);
        Assert.DoesNotContain(")", rule[5..^1]);
    }

    [Fact]
    public void A_path_cannot_append_another_host_matcher()
    {
        const string bare = "web.apps.example.com/`)||Host(`evil.com";
        const string pasted = "http://web.apps.example.com/welcome`)||Host(`evil.com";
        Assert.Equal("web.apps.example.com", PublicHostname.Normalize(bare));
        Assert.Equal("Host(`web.apps.example.com`)", PublicHostname.TraefikHostRule(bare));
        Assert.Equal("Host(`web.apps.example.com`)", PublicHostname.TraefikHostRule(pasted));
        Assert.True(PublicHostname.TryCanonical(pasted, out var hostname, out var error));
        Assert.Equal("web.apps.example.com", hostname);
        Assert.Null(error);
    }

    [Fact]
    public void Host_label_refuses_a_widened_rule()
    {
        var injection = "evil.com`)||Host(`web.apps.example.com";
        var gateway = new EdgeGateway(null!, null!, null!, null!);

        Assert.Null(PublicHostname.TraefikHostRule(injection));
        var error = Assert.Throws<ArgumentException>(() => gateway.LabelsFor("web", injection, 80));
        Assert.Equal("hostname", error.ParamName);
    }

    [Fact]
    public void International_names_are_emitted_as_punycode()
    {
        const string punycode = "xn--bcher-kva.apps.example.com";
        Assert.Equal(punycode, PublicHostname.Normalize("https://bücher.apps.example.com/path"));
        Assert.Equal(punycode, PublicHostname.Normalize("bücher.apps.example.com"));
        Assert.True(PublicHostname.IsUnder("bücher.apps.example.com", "apps.example.com"));
        Assert.Equal("Host(`" + punycode + "`)", PublicHostname.TraefikHostRule("bücher.apps.example.com"));
    }

    [Fact]
    public void Dns_label_length_limits_are_enforced()
    {
        var longestLabel = new string('a', 63) + ".example.com";
        Assert.Equal(longestLabel, PublicHostname.Normalize(longestLabel));

        Assert.Null(PublicHostname.Normalize(new string('a', 64) + ".example.com"));

        var longestName = new string('a', 63) + "." + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 61);
        Assert.Equal(253, longestName.Length);
        Assert.Equal(longestName, PublicHostname.Normalize(longestName));
        Assert.Null(PublicHostname.Normalize(longestName + "e"));
    }
}
