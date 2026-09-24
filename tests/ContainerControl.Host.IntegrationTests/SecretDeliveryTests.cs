using System.Text;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.Modules.Platform.Engine;
using Microsoft.Extensions.Configuration;

namespace ContainerControl.Host.IntegrationTests;

public sealed class SecretDeliveryTests
{
    [Fact]
    public void Deploy_plan_puts_env_secrets_in_the_environment_and_file_secrets_in_the_archive()
    {
        const string envValue = "env-secret-value";
        const string fileValue = "file-secret-value";
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: busybox:1.36.1
                environment:
                  APP_PASSWORD: ${DB_PASSWORD}
                  DIRECT: ${DB_PASSWORD}
                  LITERAL: $$not-a-secret
                command: ["myapp", "${DB_PASSWORD}"]
              worker:
                image: busybox:1.36.1
                environment:
                  - APP_PASSWORD=${DB_PASSWORD}
            """);
        Assert.True(plan.Accepted, string.Join(" ", plan.Errors));

        var injected = SecretInjection.Apply(
            plan.Services[0].Environment,
            plan.Services[0].Command,
            [("DB_PASSWORD", envValue, "env"), ("api-token", fileValue, "file")]);
        var worker = SecretInjection.Apply(
            plan.Services[1].Environment,
            plan.Services[1].Command,
            [("DB_PASSWORD", envValue, "env"), ("api-token", fileValue, "file")]);
        Assert.Equal(envValue, worker.Environment["APP_PASSWORD"]);

        Assert.Equal(envValue, injected.Environment["APP_PASSWORD"]);
        Assert.Equal(envValue, injected.Environment["DIRECT"]);
        Assert.Equal(envValue, injected.Environment["DB_PASSWORD"]);
        Assert.Equal("$not-a-secret", injected.Environment["LITERAL"]);
        Assert.Equal(envValue, injected.Command![1]);
        Assert.False(injected.Environment.ContainsKey("api-token"));
        Assert.DoesNotContain(fileValue, injected.Environment.Values);

        var archive = SecretArchive.Create(injected.Files);
        var entry = ReadFile(archive, "run/secrets/api-token");
        Assert.Equal(fileValue, entry.Content);
        Assert.Equal(0x124, entry.Mode);
        Assert.DoesNotContain(fileValue, Encoding.ASCII.GetString(archive[..512]), StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_file_name_is_rejected_without_echoing_the_value()
    {
        const string value = "do-not-echo-this-value";
        var exception = Assert.Throws<DockerEngineException>(() => SecretArchive.Create([new SecretFile("../x", value)]));
        Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Infisical_round_trip_reads_the_value_only_when_the_request_asks_for_it()
    {
        const string value = "round-trip-secret-value";
        await using var infisical = new InfisicalStandIn();
        var store = new InfisicalSecretStore(new PassthroughHttpClientFactory(), Config(infisical.BaseUrl));
        var address = new SecretAddress("dev", "/teams/abc/db-password");

        await store.WriteAsync(address, value, CancellationToken.None);
        Assert.Equal(value, await store.ReadAsync(address, CancellationToken.None));
        Assert.Equal(value, infisical.Read("dev", "db-password"));

        await store.WriteAsync(address, value + "-2", CancellationToken.None);
        Assert.Equal(value + "-2", await store.ReadAsync(address, CancellationToken.None));

        await store.DeleteAsync(address, CancellationToken.None);
        Assert.Null(infisical.Read("dev", "db-password"));
    }

    private static IConfiguration Config(string siteUrl) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Infisical:SiteUrl"] = siteUrl,
            ["Infisical:Environments:dev:ClientId"] = "client",
            ["Infisical:Environments:dev:ClientSecret"] = "secret",
            ["Infisical:Environments:dev:ProjectId"] = "project"
        }).Build();

    private static (int Mode, string Content) ReadFile(byte[] archive, string path)
    {
        var offset = 0;
        while (offset + 512 <= archive.Length)
        {
            if (archive.Skip(offset).Take(512).All(item => item == 0))
            {
                break;
            }

            var name = ReadString(archive, offset, 100);
            var prefix = ReadString(archive, offset + 345, 155);
            var full = prefix.Length == 0 ? name : prefix + "/" + name;
            var mode = Convert.ToInt32(ReadString(archive, offset + 100, 8).Trim('\0'), 8);
            var size = Convert.ToInt32(ReadString(archive, offset + 124, 12).Trim('\0'), 8);
            var content = Encoding.UTF8.GetString(archive, offset + 512, size);
            if (full == path)
            {
                return (mode, content);
            }

            offset += 512 + ((size + 511) / 512 * 512);
        }

        throw new InvalidOperationException("The archive does not contain the secret file.");
    }

    private static string ReadString(byte[] buffer, int offset, int length)
    {
        var text = Encoding.ASCII.GetString(buffer, offset, length);
        var end = text.IndexOf('\0');
        return end < 0 ? text.Trim() : text[..end];
    }

    private sealed class PassthroughHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
