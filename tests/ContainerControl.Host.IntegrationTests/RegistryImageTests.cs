using System.Reflection;
using ContainerControl.Modules.Registries.Connections;
using ContainerControl.Modules.Registries.Http;

namespace ContainerControl.Host.IntegrationTests;

public class RegistryImageTests
{
    [Theory]
    [InlineData("Acr")]
    [InlineData("Ecr")]
    [InlineData("DockerHub")]
    [InlineData("Harbor")]
    public void Supported_kinds_are_the_four_registry_types(string kind)
    {
        Assert.True(RegistryKinds.TryNormalize(kind, out var normalized));
        Assert.Equal(kind, normalized);
    }

    [Theory]
    [InlineData("registry:2")]
    [InlineData("Registry")]
    [InlineData("docker")]
    public void Other_kinds_are_rejected(string kind)
    {
        Assert.False(RegistryKinds.TryNormalize(kind, out _));
    }

    [Fact]
    public void Pull_auth_matches_the_saved_host_and_docker_hub_short_names()
    {
        Assert.Equal("docker.io", RegistryImage.HostOf("busybox:1.36.1"));
        Assert.True(RegistryImage.Matches("busybox:1.36.1", "docker.io", "DockerHub"));
        Assert.True(RegistryImage.Matches("myapp.azurecr.io/web:1", "myapp.azurecr.io", "Acr"));
        Assert.Equal("us-east-1", RegistryImage.EcrRegion("123.dkr.ecr.us-east-1.amazonaws.com"));
        Assert.False(RegistryImage.Matches("busybox:1.36.1", "harbor.example.com", "Harbor"));
    }

    [Fact]
    public void Ecr_refresh_is_due_before_the_twelve_hour_token_expires()
    {
        var now = DateTimeOffset.Parse("2026-09-24T12:00:00Z");
        Assert.True(RegistryImage.IsDue(null, now));
        Assert.True(RegistryImage.IsDue(now.AddHours(1), now));
        Assert.False(RegistryImage.IsDue(now.AddHours(6), now));
    }

    [Fact]
    public void Registry_responses_do_not_include_credential_values()
    {
        var names = typeof(RegistryResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).ToArray();
        Assert.DoesNotContain(names, name => name is "Password" or "SecretAccessKey" or "AccessKeyId");
    }
}
