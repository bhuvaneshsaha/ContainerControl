using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Edge.Domains;
using ContainerControl.SharedKernel.Hostnames;

namespace ContainerControl.Host.IntegrationTests;

public sealed class ServiceHostnameTests
{
    [Fact]
    public void Omitted_service_hostname_uses_the_application_hostname()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
                  hostname: "  "
            """);

        Assert.True(plan.Accepted);
        var service = Assert.Single(plan.Services);
        Assert.Null(service.Hostname);
        Assert.Equal("my-app.apps.example.com", ComposePolicy.RouteHostname(service, "https://My-App.apps.example.com/welcome"));
        Assert.Null(ComposePolicy.DuplicatePublicHostname(plan.Services, "my-app.apps.example.com"));

        var rule = LabelsFor(service, "https://My-App.apps.example.com/welcome");
        Assert.Equal("Host(`my-app.apps.example.com`)", rule);
    }

    [Fact]
    public void Service_hostname_overrides_the_application_hostname()
    {
        var plan = ComposePolicy.Parse("""
            services:
              api:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 8080
                  hostname: https://API-A.apps.localhost/path
            """);

        Assert.True(plan.Accepted);
        var service = Assert.Single(plan.Services);
        Assert.Equal("api-a.apps.localhost", service.Hostname);
        Assert.Equal("api-a.apps.localhost", ComposePolicy.RouteHostname(service, "web.apps.localhost"));

        var rule = LabelsFor(service, "web.apps.localhost");
        Assert.Equal("Host(`api-a.apps.localhost`)", rule);
    }

    [Theory]
    [InlineData("not a host")]
    [InlineData("*.apps.localhost")]
    [InlineData("evil.com`)||Host(`ok.apps.localhost")]
    [InlineData("api..apps.localhost")]
    [InlineData("-bad.apps.localhost")]
    public void Invalid_service_hostname_uses_the_public_hostname_error(string hostname)
    {
        var plan = ComposePolicy.Parse($$"""
            services:
              api:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
                  hostname: "{{hostname}}"
            """);

        Assert.False(plan.Accepted);
        Assert.Empty(plan.Services);
        Assert.Contains($"Service 'api': {PublicHostname.InvalidMessage}", plan.Errors);
    }

    [Fact]
    public void A_hostname_mapping_is_rejected()
    {
        var plan = ComposePolicy.Parse("""
            services:
              api:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
                  hostname:
                    name: api.apps.localhost
            """);

        Assert.False(plan.Accepted);
        Assert.Contains($"Service 'api': {PublicHostname.InvalidMessage}", plan.Errors);
    }

    [Fact]
    public void Duplicate_service_hostnames_are_rejected()
    {
        var plan = ComposePolicy.Parse("""
            services:
              api-a:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
                  hostname: HTTPS://API.apps.localhost/a
              api-b:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
                  hostname: api.apps.localhost
            """);

        Assert.False(plan.Accepted);
        Assert.Empty(plan.Services);
        Assert.Contains("Services 'api-a' and 'api-b' use the same public hostname 'api.apps.localhost'.", plan.Errors);
    }

    [Fact]
    public void Explicit_hostname_colliding_with_the_application_hostname_is_rejected()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
              api:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 8080
                  hostname: https://Web.apps.localhost/home
            """);

        Assert.True(plan.Accepted);
        var error = ComposePolicy.DuplicatePublicHostname(plan.Services, "web.apps.localhost");
        Assert.Equal(
            "Service 'api' uses public hostname 'web.apps.localhost', which is also the hostname for 'web'.",
            error);
    }

    [Fact]
    public void An_internal_hostname_cannot_match_another_services_effective_hostname()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
              cache:
                image: busybox:1.36.1
                x-containercontrol:
                  hostname: web.apps.localhost
            """);

        Assert.True(plan.Accepted);
        Assert.Equal(
            "Service 'cache' uses public hostname 'web.apps.localhost', which is also the hostname for 'web'.",
            ComposePolicy.DuplicatePublicHostname(plan.Services, "WEB.apps.localhost"));
        Assert.Null(ComposePolicy.RouteHostname(plan.Services.Single(service => service.Name == "cache"), "web.apps.localhost"));
    }

    [Fact]
    public void Shared_application_hostname_is_still_allowed()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
              extra:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 8080
            """);

        Assert.True(plan.Accepted);
        Assert.Null(ComposePolicy.DuplicatePublicHostname(plan.Services, "app.apps.example.com"));
        Assert.All(plan.Services, service =>
            Assert.Equal("app.apps.example.com", ComposePolicy.RouteHostname(service, "app.apps.example.com")));
        Assert.Equal(
            "Host(`app.apps.example.com`)",
            LabelsFor(plan.Services[0], "app.apps.example.com"));
        Assert.Equal(
            "Host(`app.apps.example.com`)",
            LabelsFor(plan.Services[1], "app.apps.example.com"));
    }

    [Fact]
    public void Two_services_keep_distinct_hostnames_and_an_internal_service_stays_unpublished()
    {
        var plan = ComposePolicy.Parse("""
            services:
              api-a:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
                  hostname: api-a.apps.localhost
              api-b:
                image: nginx:stable
                x-containercontrol:
                  exposed: true
                  port: 80
                  hostname: api-b.apps.localhost
              worker:
                image: busybox:1.36.1
              cache:
                image: busybox:1.36.1
                x-containercontrol:
                  hostname: cache.apps.localhost
            """);

        Assert.True(plan.Accepted, string.Join(" ", plan.Errors));
        Assert.Null(ComposePolicy.DuplicatePublicHostname(plan.Services, null));
        Assert.Null(ComposePolicy.DuplicatePublicHostname(plan.Services, "apps.localhost"));

        var apiA = plan.Services.Single(service => service.Name == "api-a");
        var apiB = plan.Services.Single(service => service.Name == "api-b");
        var worker = plan.Services.Single(service => service.Name == "worker");
        var cache = plan.Services.Single(service => service.Name == "cache");

        Assert.Equal("api-a.apps.localhost", ComposePolicy.RouteHostname(apiA, null));
        Assert.Equal("api-b.apps.localhost", ComposePolicy.RouteHostname(apiB, "web.apps.localhost"));
        Assert.Null(worker.Hostname);
        Assert.Null(ComposePolicy.RouteHostname(worker, null));
        Assert.Null(ComposePolicy.RouteHostname(worker, "web.apps.localhost"));
        Assert.Equal("cache.apps.localhost", cache.Hostname);
        Assert.Null(ComposePolicy.RouteHostname(cache, "web.apps.localhost"));

        var gateway = new EdgeGateway(null!, null!, null!, null!);
        var first = gateway.LabelsFor("apia", ComposePolicy.RouteHostname(apiA, null)!, apiA.Port!.Value);
        var second = gateway.LabelsFor("apib", ComposePolicy.RouteHostname(apiB, null)!, apiB.Port!.Value);
        Assert.Equal("Host(`api-a.apps.localhost`)", first["traefik.http.routers.apia.rule"]);
        Assert.Equal("Host(`api-b.apps.localhost`)", second["traefik.http.routers.apib.rule"]);
        Assert.Equal("80", first["traefik.http.services.apia.loadbalancer.server.port"]);
        Assert.Equal("80", second["traefik.http.services.apib.loadbalancer.server.port"]);
    }

    private static string? LabelsFor(PlannedService service, string? applicationHostname)
    {
        var host = ComposePolicy.RouteHostname(service, applicationHostname);
        if (host is null || service.Port is null)
        {
            return null;
        }

        var gateway = new EdgeGateway(null!, null!, null!, null!);
        var labels = gateway.LabelsFor(service.Name, host, service.Port.Value);
        return labels["traefik.http.routers." + service.Name + ".rule"];
    }
}
