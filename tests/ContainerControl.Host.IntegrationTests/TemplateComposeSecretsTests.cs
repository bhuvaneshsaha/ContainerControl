using ContainerControl.Modules.Applications.Templates;
using ContainerControl.Modules.Delivery.Compose;

namespace ContainerControl.Host.IntegrationTests;

public class TemplateComposeSecretsTests
{
    private const string SafeCompose = """
        services:
          web:
            image: nginx:stable
            environment:
              GREETING: hello
              DATABASE_PASSWORD: ${DATABASE_PASSWORD}
            x-containercontrol:
              exposed: true
              port: 80
        """;

    [Fact]
    public void A_policy_compliant_compose_with_placeholders_is_accepted()
    {
        Assert.Empty(TemplateComposeSecrets.FindInCompose(SafeCompose));
        var gate = new ComposePolicyGate().Accept(SafeCompose);
        Assert.True(gate.Accepted);
        Assert.Empty(gate.Errors);
    }

    [Theory]
    [InlineData("API_TOKEN: super-secret-value")]
    [InlineData("DATABASE_PASSWORD=super-secret-value")]
    [InlineData("postgres://app:super-secret-value@db/app")]
    public void Secret_values_are_rejected_without_repeating_them(string assignment)
    {
        var yaml = "services:\n  web:\n    image: nginx:stable\n    environment:\n      " + assignment + "\n";
        var errors = TemplateComposeSecrets.FindInCompose(yaml);
        Assert.Contains(TemplateComposeSecrets.SecretValueMessage, errors);
        Assert.DoesNotContain("super-secret-value", string.Join('\n', errors), StringComparison.Ordinal);
        var gate = new ComposePolicyGate().Accept(yaml);
        Assert.False(gate.Accepted);
        Assert.DoesNotContain("super-secret-value", string.Join('\n', gate.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void A_private_key_is_rejected_without_repeating_it()
    {
        const string material = "AAAA-PRIVATE-KEY-MATERIAL";
        var yaml = "services:\n  web:\n    image: nginx:stable\n    environment:\n      KEY: |\n        -----BEGIN PRIVATE KEY-----\n        " + material + "\n";
        var errors = TemplateComposeSecrets.FindInCompose(yaml);
        Assert.Contains(TemplateComposeSecrets.PrivateKeyMessage, errors);
        Assert.DoesNotContain(material, string.Join('\n', errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Env_file_and_compose_secrets_are_rejected()
    {
        var yaml = """
            services:
              web:
                image: nginx:stable
                env_file: .env
                secrets:
                  - db
            """;
        var errors = TemplateComposeSecrets.FindInCompose(yaml);
        Assert.Contains(TemplateComposeSecrets.EnvFileMessage, errors);
        Assert.Contains(TemplateComposeSecrets.ComposeSecretsMessage, errors);
    }

    [Fact]
    public void Privileged_compose_fails_the_existing_policy()
    {
        var yaml = """
            services:
              web:
                image: nginx:stable
                privileged: true
            """;
        var gate = new ComposePolicyGate().Accept(yaml);
        Assert.False(gate.Accepted);
        Assert.Contains(gate.Errors, error => error.Contains("privileged", StringComparison.Ordinal));
    }

    [Fact]
    public void Name_and_description_reject_a_pasted_secret()
    {
        var errors = TemplateComposeSecrets.Find("password=super-secret-value");
        Assert.Contains(TemplateComposeSecrets.SecretValueMessage, errors);
        Assert.DoesNotContain("super-secret-value", string.Join('\n', errors), StringComparison.Ordinal);
        Assert.Empty(TemplateComposeSecrets.Find("A web server."));
    }
}
