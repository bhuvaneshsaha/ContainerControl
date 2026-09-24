using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Quotas;
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
    IReadOnlyList<string> DependsOn,
    ContainerHealthcheck? Healthcheck = null,
    ServiceResources? Resources = null);

public sealed record ComposePlan(bool Accepted, IReadOnlyList<string> Errors, IReadOnlyList<PlannedService> Services);

public static class ComposePolicy
{
    // Whole tokens only. An exact leaf or "name-" prefix misses vendor names
    // (postgresql, timescaledb) and parent paths (mssql/server). Token boundaries
    // keep postgrest, phpmyadmin, and oraclelinux allowed.
    private static readonly HashSet<string> DatabaseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgres", "postgresql", "postgis", "pgvector", "timescale", "timescaledb", "citus",
        "mysql", "mariadb", "percona",
        "mongo", "mongodb",
        "mssql", "sqlserver",
        "cassandra", "scylla", "scylladb",
        "cockroach", "cockroachdb",
        "clickhouse",
        "elasticsearch", "opensearch",
        "influxdb",
        "neo4j",
        "couchdb", "couchbase"
    };

    // Hyphenated product names that are not a single token in DatabaseTokens.
    private static readonly string[] DatabasePhrases =
    [
        "pgvecto-rs",
        "azure-sql-edge",
        "sql-server",
        "database-enterprise",
        "database-express",
        "database-standard",
        "database-free",
        "database-personal"
    ];

    private static readonly HashSet<string> OracleEditionTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "enterprise", "express", "free", "standard", "personal"
    };

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
                errors.Add($"Service '{name}' uses database image '{image}'. Use the data tier.");
            }

            var extension = Child(body, "x-containercontrol") as YamlMappingNode;
            var exposed = Text(extension is null ? null : Child(extension, "exposed")).Equals("true", StringComparison.OrdinalIgnoreCase);
            int? port = null;
            if (int.TryParse(Text(extension is null ? null : Child(extension, "port")), out var parsedPort))
            {
                port = parsedPort;
            }

            var healthcheck = ReadHealthcheck(Child(body, "healthcheck"), name, errors);
            services.Add(new PlannedService(
                name,
                image,
                ReadCommand(Child(body, "command")),
                exposed,
                port,
                ReadEnvironment(Child(body, "environment")),
                ReadDepends(Child(body, "depends_on")),
                healthcheck,
                ReadResources(body, name, errors)));
        }

        if (services.Count == 0 && errors.Count == 0)
        {
            errors.Add("The compose file has no services.");
        }

        var names = services.Select(service => service.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var service in services)
        {
            foreach (var dependency in service.DependsOn)
            {
                if (!names.Contains(dependency))
                {
                    errors.Add($"Service '{service.Name}' depends on '{dependency}', which is not in the file.");
                }
            }
        }

        if (errors.Count == 0 && HasCycle(services))
        {
            errors.Add("Service dependencies contain a cycle.");
        }

        return new ComposePlan(errors.Count == 0, errors, errors.Count == 0 ? services : []);
    }

    public static IReadOnlyList<PlannedService> StartOrder(IReadOnlyList<PlannedService> services)
    {
        var pending = services.ToList();
        var ordered = new List<PlannedService>();
        while (pending.Count > 0)
        {
            var nextIndex = pending.FindIndex(service =>
                service.DependsOn.All(name => ordered.Any(done => done.Name == name)));
            if (nextIndex < 0)
            {
                throw new InvalidOperationException("Service dependencies contain a cycle.");
            }

            ordered.Add(pending[nextIndex]);
            pending.RemoveAt(nextIndex);
        }

        return ordered;
    }

    public static bool IsDatabaseImage(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return false;
        }

        var segments = RepositorySegments(image);
        if (segments.Length == 0)
        {
            return false;
        }

        // A leading registry host is not a product name. "oracle" is matched on the
        // leaf only so Oracle Linux and Instant Client are not treated as databases.
        var firstName = IsRegistryHost(segments[0]) && segments.Length > 1 ? 1 : 0;
        for (var index = firstName; index < segments.Length; index++)
        {
            if (NamesDatabase(segments[index], leaf: index == segments.Length - 1))
            {
                return true;
            }
        }

        return IsOracleRegistryDatabase(segments);
    }

    private static string[] RepositorySegments(string image)
    {
        var name = image.Trim();
        var digest = name.IndexOf('@');
        if (digest >= 0)
        {
            name = name[..digest];
        }

        var slash = name.LastIndexOf('/');
        var colon = name.LastIndexOf(':');
        if (colon > slash)
        {
            name = name[..colon];
        }

        return name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsRegistryHost(string segment) =>
        segment.Contains('.')
        || segment.Contains(':')
        || segment.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static bool NamesDatabase(string segment, bool leaf)
    {
        var normalized = segment.Replace('_', '-');
        foreach (var phrase in DatabasePhrases)
        {
            if (ContainsHyphenPhrase(normalized, phrase))
            {
                return true;
            }
        }

        foreach (var token in normalized.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            if (leaf && token.Equals("oracle", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (DatabaseTokens.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsHyphenPhrase(string segment, string phrase)
    {
        if (segment.Equals(phrase, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ("-" + segment + "-").Contains("-" + phrase + "-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOracleRegistryDatabase(string[] segments)
    {
        if (segments.Length < 3 || !IsOracleRegistryHost(segments[0]))
        {
            return false;
        }

        var hasDatabaseSegment = false;
        for (var index = 1; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("database", StringComparison.OrdinalIgnoreCase))
            {
                hasDatabaseSegment = true;
                break;
            }
        }

        if (!hasDatabaseSegment)
        {
            return false;
        }

        var leaf = segments[^1].Replace('_', '-');
        return leaf.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Any(token => OracleEditionTokens.Contains(token));
    }

    private static bool IsOracleRegistryHost(string segment)
    {
        var colon = segment.LastIndexOf(':');
        var host = colon > 0 ? segment[..colon] : segment;
        return host.Equals("oracle.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".oracle.com", StringComparison.OrdinalIgnoreCase);
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
                var key = Text(entry.Key);
                if (key.Length > 0)
                {
                    values[key] = Text(entry.Value);
                }
            }
        }

        if (node is YamlSequenceNode sequence)
        {
            foreach (var item in sequence.Children)
            {
                var text = Text(item);
                var split = text.IndexOf('=');
                if (split > 0)
                {
                    values[text[..split]] = text[(split + 1)..];
                }
            }
        }

        return values;
    }

    private static ContainerHealthcheck? ReadHealthcheck(YamlNode? node, string service, List<string> errors)
    {
        if (node is not YamlMappingNode map)
        {
            return null;
        }

        if (Text(Child(map, "disable")).Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var testNode = Child(map, "test");
        IReadOnlyList<string> test;
        if (testNode is YamlSequenceNode sequence)
        {
            test = sequence.Children.Select(Text).Where(item => item.Length > 0).ToArray();
        }
        else
        {
            var text = Text(testNode);
            test = string.IsNullOrWhiteSpace(text) ? [] : ["CMD-SHELL", text];
        }

        if (test.Count == 0)
        {
            errors.Add($"Service '{service}' healthcheck needs a test.");
            return null;
        }

        if (test[0].Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!TryDuration(Text(Child(map, "interval")), TimeSpan.FromSeconds(30), out var interval))
        {
            errors.Add($"Service '{service}' healthcheck interval is not a duration.");
        }

        if (!TryDuration(Text(Child(map, "timeout")), TimeSpan.FromSeconds(30), out var timeout))
        {
            errors.Add($"Service '{service}' healthcheck timeout is not a duration.");
        }

        if (!TryDuration(Text(Child(map, "start_period")), TimeSpan.Zero, out var startPeriod))
        {
            errors.Add($"Service '{service}' healthcheck start_period is not a duration.");
        }

        var retriesText = Text(Child(map, "retries"));
        var retries = 3;
        if (!string.IsNullOrWhiteSpace(retriesText) && (!int.TryParse(retriesText, out retries) || retries < 1))
        {
            errors.Add($"Service '{service}' healthcheck retries must be a positive number.");
            retries = 3;
        }

        return errors.Count == 0
            ? new ContainerHealthcheck(test, interval, timeout, startPeriod, retries)
            : null;
    }

    private static bool TryDuration(string text, TimeSpan fallback, out TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            duration = fallback;
            return true;
        }

        var total = TimeSpan.Zero;
        var index = 0;
        var matched = false;
        while (index < text.Length)
        {
            var start = index;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            if (start == index || !int.TryParse(text[start..index], out var number))
            {
                duration = fallback;
                return false;
            }

            var unitStart = index;
            while (index < text.Length && char.IsLetter(text[index]))
            {
                index++;
            }

            if (unitStart == index)
            {
                duration = fallback;
                return false;
            }

            var unit = text[unitStart..index];
            var piece = unit switch
            {
                "ms" => TimeSpan.FromMilliseconds(number),
                "s" => TimeSpan.FromSeconds(number),
                "m" => TimeSpan.FromMinutes(number),
                "h" => TimeSpan.FromHours(number),
                _ => TimeSpan.MinValue
            };
            if (piece < TimeSpan.Zero)
            {
                duration = fallback;
                return false;
            }

            total += piece;
            matched = true;
        }

        duration = matched ? total : fallback;
        return matched;
    }

    private static bool HasCycle(IReadOnlyList<PlannedService> services)
    {
        var byName = services.ToDictionary(service => service.Name, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string name)
        {
            if (visited.Contains(name))
            {
                return false;
            }

            if (!visiting.Add(name))
            {
                return true;
            }

            if (byName.TryGetValue(name, out var service))
            {
                foreach (var dependency in service.DependsOn)
                {
                    if (byName.ContainsKey(dependency) && Visit(dependency))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(name);
            visited.Add(name);
            return false;
        }

        return services.Any(service => Visit(service.Name));
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

    private static ServiceResources? ReadResources(YamlMappingNode body, string name, List<string> errors)
    {
        if (Child(body, "deploy") is not YamlMappingNode deploy
            || Child(deploy, "resources") is not YamlMappingNode resources
            || Child(resources, "limits") is not YamlMappingNode limits)
        {
            return null;
        }

        if (!ResourceQuantity.TryParseCpus(Text(Child(limits, "cpus")), out var cpu)
            || !ResourceQuantity.TryParseBytes(Text(Child(limits, "memory")), out var memory)
            || !ResourceQuantity.TryParseBytes(Text(Child(limits, "storage")), out var storage))
        {
            errors.Add($"Service '{name}' needs CPU, memory, and storage limits, such as cpus: \"0.5\", memory: 256M, and storage: 1G.");
            return null;
        }

        return new ServiceResources(cpu, memory, storage);
    }

    private static YamlNode? Child(YamlMappingNode node, string key) =>
        node.Children.FirstOrDefault(entry => Text(entry.Key) == key).Value;

    private static string Text(YamlNode? node) =>
        node is YamlScalarNode scalar ? scalar.Value ?? string.Empty : string.Empty;
}
