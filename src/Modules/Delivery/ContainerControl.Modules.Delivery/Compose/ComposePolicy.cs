using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ContainerControl.Modules.Delivery.Compose;

public sealed record PlannedService(
    string Name,
    string Image,
    IReadOnlyList<string>? Command,
    bool Exposed,
    int? Port,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> DependsOn);

public sealed record ComposePlan(bool Accepted, IReadOnlyList<string> Errors, IReadOnlyList<PlannedService> Services);

public static class ComposePolicy
{
    private static readonly string[] DatabaseMarkers =
    [
        "postgres", "mysql", "mariadb", "mongo", "mongodb", "mssql", "sqlserver",
        "oracle", "cassandra", "cockroach", "clickhouse", "elasticsearch", "opensearch",
        "influxdb", "neo4j", "couchdb", "percona", "timescale"
    ];

    public static ComposePlan FromImage(string image, IReadOnlyList<string>? command, bool exposed, int? port)
    {
        var errors = new List<string>();
        if (IsDatabaseImage(image))
        {
            errors.Add($"Image '{image}' is a database image. Use the data tier.");
        }

        var services = errors.Count == 0
            ? new[] { new PlannedService("app", image, command, exposed, port, new Dictionary<string, string>(), []) }
            : Array.Empty<PlannedService>();
        return new ComposePlan(errors.Count == 0, errors, services);
    }

    public static ComposePlan Parse(string yaml)
    {
        var errors = new List<string>();
        var services = new List<PlannedService>();
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException)
        {
            return new ComposePlan(false, ["The compose file is not valid YAML."], []);
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return new ComposePlan(false, ["The compose file has no services."], []);
        }

        var servicesNode = Child(root, "services") as YamlMappingNode;
        if (servicesNode is null)
        {
            return new ComposePlan(false, ["The compose file has no services."], []);
        }

        foreach (var entry in servicesNode.Children)
        {
            var name = Text(entry.Key);
            if (entry.Value is not YamlMappingNode body)
            {
                errors.Add($"Service '{name}' is not a mapping.");
                continue;
            }

            RejectFlag(body, name, "privileged", errors);
            RejectHost(body, name, "network_mode", errors);
            RejectHost(body, name, "pid", errors);
            RejectHost(body, name, "ipc", errors);
            RejectPresent(body, name, "cap_add", errors);
            RejectPresent(body, name, "devices", errors);
            RejectPresent(body, name, "build", errors);
            RejectVolumes(body, name, errors);

            var image = Text(Child(body, "image"));
            if (string.IsNullOrWhiteSpace(image))
            {
                errors.Add($"Service '{name}' needs an image. Build is not allowed.");
                continue;
            }

            if (IsDatabaseImage(image))
            {
                errors.Add($"Service '{name}' uses database image '{image}'.");
            }

            var extension = Child(body, "x-containercontrol") as YamlMappingNode;
            var exposed = Text(extension is null ? null : Child(extension, "exposed")).Equals("true", StringComparison.OrdinalIgnoreCase);
            int? port = null;
            if (int.TryParse(Text(extension is null ? null : Child(extension, "port")), out var parsedPort))
            {
                port = parsedPort;
            }

            services.Add(new PlannedService(
                name,
                image,
                ReadCommand(Child(body, "command")),
                exposed,
                port,
                ReadEnvironment(Child(body, "environment")),
                ReadDepends(Child(body, "depends_on"))));
        }

        if (services.Count == 0 && errors.Count == 0)
        {
            errors.Add("The compose file has no services.");
        }

        return new ComposePlan(errors.Count == 0, errors, errors.Count == 0 ? services : []);
    }

    public static bool IsDatabaseImage(string image)
    {
        var name = image.Split('@')[0];
        var slash = name.LastIndexOf('/');
        var colon = name.LastIndexOf(':');
        var repo = colon > slash ? name[..colon] : name;
        var leaf = repo[(repo.LastIndexOf('/') + 1)..];
        return DatabaseMarkers.Any(marker => leaf.Equals(marker, StringComparison.OrdinalIgnoreCase)
            || leaf.StartsWith(marker + "-", StringComparison.OrdinalIgnoreCase));
    }

    private static void RejectFlag(YamlMappingNode body, string service, string key, List<string> errors)
    {
        if (Text(Child(body, key)).Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Service '{service}' sets {key}, which is not allowed.");
        }
    }

    private static void RejectHost(YamlMappingNode body, string service, string key, List<string> errors)
    {
        if (Text(Child(body, key)).Equals("host", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Service '{service}' sets {key}: host, which is not allowed.");
        }
    }

    private static void RejectPresent(YamlMappingNode body, string service, string key, List<string> errors)
    {
        if (Child(body, key) is not null)
        {
            errors.Add($"Service '{service}' sets {key}, which is not allowed.");
        }
    }

    private static void RejectVolumes(YamlMappingNode body, string service, List<string> errors)
    {
        var volumes = Child(body, "volumes");
        if (volumes is YamlSequenceNode sequence)
        {
            foreach (var item in sequence.Children)
            {
                if (item is YamlScalarNode scalar && IsBind(scalar.Value ?? string.Empty))
                {
                    errors.Add($"Service '{service}' uses a bind mount, which is not allowed.");
                }

                if (item is YamlMappingNode map && Text(Child(map, "type")).Equals("bind", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Service '{service}' uses a bind mount, which is not allowed.");
                }
            }
        }
    }

    private static bool IsBind(string volume)
    {
        if (volume.Contains("docker.sock", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var split = volume.IndexOf(':');
        if (split <= 0)
        {
            return false;
        }

        var source = volume[..split];
        return source.StartsWith('/') || source.StartsWith('.') || source.StartsWith('~');
    }

    private static IReadOnlyList<string>? ReadCommand(YamlNode? node)
    {
        if (node is YamlSequenceNode sequence)
        {
            return sequence.Children.Select(Text).Where(item => item.Length > 0).ToArray();
        }

        var text = Text(node);
        return string.IsNullOrWhiteSpace(text) ? null : text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyDictionary<string, string> ReadEnvironment(YamlNode? node)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (node is YamlMappingNode map)
        {
            foreach (var entry in map.Children)
            {
                values[Text(entry.Key)] = Text(entry.Value);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> ReadDepends(YamlNode? node)
    {
        if (node is YamlSequenceNode sequence)
        {
            return sequence.Children.Select(Text).Where(item => item.Length > 0).ToArray();
        }

        if (node is YamlMappingNode map)
        {
            return map.Children.Select(entry => Text(entry.Key)).ToArray();
        }

        return [];
    }

    private static YamlNode? Child(YamlMappingNode node, string key) =>
        node.Children.FirstOrDefault(entry => Text(entry.Key) == key).Value;

    private static string Text(YamlNode? node) =>
        node is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;
}
