using ContainerControl.Modules.Delivery.Compose;

namespace ContainerControl.Host.IntegrationTests;

public class ComposeProjectNameTests
{
    private static readonly Guid AppId = Guid.Parse("97f94c8a-3d8e-49ee-9397-2a358eb9a636");

    [Theory]
    [InlineData("Billing Portal", "billing-portal")]
    [InlineData("Hello_World", "hello_world")]
    [InlineData("Hello---World", "hello---world")]
    [InlineData("  My App!!!  ", "my-app")]
    [InlineData("123 Go", "123-go")]
    [InlineData("__Hello__", "hello")]
    [InlineData("a!!b", "a-b")]
    public void Sanitize_keeps_compose_project_characters(string value, string expected)
    {
        Assert.Equal(expected, ComposeProjectName.Sanitize(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("___")]
    public void Sanitize_is_empty_when_nothing_valid_remains(string? value)
    {
        Assert.Equal(string.Empty, ComposeProjectName.Sanitize(value));
    }

    [Fact]
    public void Sanitize_caps_length_and_drops_a_trailing_separator()
    {
        Assert.Equal(new string('a', ComposeProjectName.MaxLength), ComposeProjectName.Sanitize(new string('a', 80)));

        var cutAtSeparator = ComposeProjectName.Sanitize(new string('a', 60) + "---" + new string('b', 10));

        Assert.Equal(new string('a', 60), cutAtSeparator);
        Assert.Matches("^[a-z0-9][a-z0-9_-]*$", cutAtSeparator);
    }

    [Fact]
    public void Resolve_uses_the_application_name_when_compose_has_no_name()
    {
        const string yaml = """
            services:
              web:
                image: nginx:1.27
            """;

        Assert.Equal("billing-portal", ComposeProjectName.Resolve(yaml, "Billing Portal", AppId));
        Assert.Equal("billing-portal", ComposeProjectName.Resolve(null, "Billing Portal", AppId));
    }

    [Fact]
    public void Resolve_prefers_the_compose_name_over_the_application_name()
    {
        const string yaml = """
            name: Web Stack
            services:
              web:
                image: nginx:1.27
            """;

        Assert.Equal("web-stack", ComposeProjectName.Resolve(yaml, "Billing Portal", AppId));
    }

    [Fact]
    public void Resolve_falls_back_when_the_chosen_name_sanitizes_to_empty()
    {
        const string named = """
            name: "!!!"
            services:
              web:
                image: nginx:1.27
            """;

        Assert.Equal(ComposeProjectName.Fallback(AppId), ComposeProjectName.Resolve(named, "Billing Portal", AppId));
        Assert.Equal(ComposeProjectName.Fallback(AppId), ComposeProjectName.Resolve(null, "!!!", AppId));
        Assert.Equal("cc-app-97f94c8a", ComposeProjectName.Fallback(AppId));
    }

    [Fact]
    public void Resolve_uses_the_application_name_when_compose_yaml_is_invalid()
    {
        Assert.Equal("billing-portal", ComposeProjectName.Resolve("name: [", "Billing Portal", AppId));
    }

    [Fact]
    public void Stamp_sets_the_desktop_project_and_service_labels()
    {
        var labels = new Dictionary<string, string>();

        ComposeProjectName.Stamp(labels, "billing-portal", "web");

        Assert.Equal("billing-portal", labels["com.docker.compose.project"]);
        Assert.Equal("web", labels["com.docker.compose.service"]);
        Assert.Equal("1", labels["com.docker.compose.container-number"]);
        Assert.Equal("False", labels["com.docker.compose.oneoff"]);
    }
}
