using ContainerControl.Modules.Platform.Persistence;
using ContainerControl.Modules.Platform.Quotas;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContainerControl.Modules.Platform.Engine;

public sealed class DockerEngineClient : IDockerEngine
{
    private readonly bool _allowCleartextTcp;
    private readonly IServiceScopeFactory? _scopes;

    public DockerEngineClient()
    {
    }

    [ActivatorUtilitiesConstructor]
    public DockerEngineClient(IHostEnvironment environment, IServiceScopeFactory scopes)
    {
        _allowCleartextTcp = environment.IsDevelopment();
        _scopes = scopes;
    }

    public async Task<EngineVersion> GetVersionAsync(DockerEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var version = await client.System.GetVersionAsync(cancellationToken);
        return new EngineVersion(version.Version, version.APIVersion);
    }

    public async Task<HostCapacity> ReadCapacityAsync(DockerEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var info = await client.System.GetSystemInfoAsync(cancellationToken);
        return new HostCapacity(info.NCPU, info.MemTotal, HostCapacityText.DataSpaceBytes(info.DriverStatus));
    }

    public async Task<IReadOnlyList<string>> ListContainerIdsAsync(DockerEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken);
        return containers.Select(container => container.ID).ToArray();
    }

    public async Task EnsureNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var existing = await client.Networks.ListNetworksAsync(new NetworksListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [name] = true }
            }
        }, cancellationToken);
        if (existing.Any(network => string.Equals(network.Name, name, StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            await client.Networks.CreateNetworkAsync(new NetworksCreateParameters
            {
                Name = name,
                Driver = "bridge",
                CheckDuplicate = true,
                IPAM = AppSubnet(name) is { } subnet
                    ? new IPAM { Config = [new IPAMConfig { Subnet = subnet }] }
                    : null
            }, cancellationToken);
        }
        catch (DockerApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
        }
    }

    public async Task RemoveNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        try
        {
            await client.Networks.DeleteNetworkAsync(name, cancellationToken);
        }
        catch (DockerApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }
    }

    public async Task PullImageAsync(DockerEndpoint endpoint, string image, ImagePullAuth? auth, CancellationToken cancellationToken)
    {
        var (fromImage, tag) = SplitImage(image);
        using var client = await ConnectAsync(endpoint, cancellationToken);
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = fromImage, Tag = tag },
            auth is null
                ? null
                : new AuthConfig { Username = auth.Username, Password = auth.Password, ServerAddress = auth.Server },
            new Progress<JSONMessage>(),
            cancellationToken);
    }

    public async Task<string> CreateContainerAsync(DockerEndpoint endpoint, ContainerPlan plan, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var labels = new Dictionary<string, string>(plan.Labels, StringComparer.Ordinal);
        var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = plan.Name,
            Image = plan.Image,
            Cmd = plan.Command?.ToList(),
            Env = plan.Environment.Select(pair => pair.Key + "=" + pair.Value).ToList(),
            Labels = labels,
            Healthcheck = ToHealthcheck(plan.Healthcheck),
            HostConfig = new HostConfig
            {
                Binds = plan.Binds.ToList(),
                PortBindings = plan.PublishedPorts.ToDictionary(
                    pair => pair.Key,
                    pair => (IList<PortBinding>)new List<PortBinding> { new() { HostPort = pair.Value } }),
                RestartPolicy = new RestartPolicy { Name = ToRestart(plan.RestartPolicy) },
                NanoCPUs = plan.NanoCpus,
                Memory = plan.MemoryLimit
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [plan.Network] = new EndpointSettings { Aliases = [plan.NetworkAlias] }
                }
            },
            ExposedPorts = plan.PublishedPorts.Keys.ToDictionary(key => key, _ => default(EmptyStruct))
        }, cancellationToken);

        foreach (var network in plan.ExtraNetworks)
        {
            await client.Networks.ConnectNetworkAsync(network, new NetworkConnectParameters
            {
                Container = response.ID,
                EndpointConfig = new EndpointSettings { Aliases = [plan.NetworkAlias] }
            }, cancellationToken);
        }

        return response.ID;
    }

    public async Task ExtractArchiveAsync(
        DockerEndpoint endpoint,
        string containerId,
        string destinationPath,
        Stream archive,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        await client.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new ContainerPathStatParameters { Path = destinationPath },
            archive,
            cancellationToken);
    }

    public async Task StartContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);
    }

    public async Task StopContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 5 }, cancellationToken);
    }

    public async Task RemoveContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<EngineContainer>> ListByLabelAsync(
        DockerEndpoint endpoint,
        string label,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [label] = true }
            }
        }, cancellationToken);

        return containers.Select(container =>
        {
            var ip = container.NetworkSettings?.Networks?.Values.FirstOrDefault()?.IPAddress;
            IReadOnlyDictionary<string, string>? labels = container.Labels is null
                ? null
                : new Dictionary<string, string>(container.Labels, StringComparer.Ordinal);
            return new EngineContainer(container.ID, container.Names.FirstOrDefault() ?? container.ID, container.State == "running", ip, labels);
        }).ToArray();
    }

    public async Task<string> ExecAsync(
        DockerEndpoint endpoint,
        string containerId,
        IReadOnlyList<string> command,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var created = await client.Exec.CreateContainerExecAsync(containerId, new ContainerExecCreateParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            Cmd = command.ToList()
        }, cancellationToken);
        using var stream = await client.Exec.StartContainerExecAsync(created.ID, new ContainerExecStartParameters
        {
            Detach = false,
            Tty = false
        }, cancellationToken);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
        var inspect = await client.Exec.InspectContainerExecAsync(created.ID, cancellationToken);
        if (inspect.ExitCode != 0)
        {
            var detail = (stdout + stderr).ReplaceLineEndings(" ").Trim();
            if (detail.Length > 180)
            {
                detail = detail[..180];
            }

            throw new DockerEngineException(string.IsNullOrWhiteSpace(detail)
                ? "A command inside the container failed."
                : "A command inside the container failed. " + detail);
        }

        return stdout + stderr;
    }

    public async Task<string> ReadLogsAsync(
        DockerEndpoint endpoint,
        string containerId,
        int tail,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        using var stream = await client.Containers.GetContainerLogsAsync(containerId, new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Tail = tail.ToString()
        }, cancellationToken);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
        return stdout + stderr;
    }

    public async Task<string> ReadTimestampedLogsAsync(
        DockerEndpoint endpoint,
        string containerId,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        using var stream = await client.Containers.GetContainerLogsAsync(containerId, new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Timestamps = true,
            Tail = "400",
            Since = since is null ? null : since.Value.ToUnixTimeSeconds().ToString()
        }, cancellationToken);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
        return stdout + stderr;
    }

    public async Task FollowLogsAsync(
        DockerEndpoint endpoint,
        string containerId,
        int tail,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        await client.Containers.GetContainerLogsAsync(
            containerId,
            new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = true,
                Tail = tail.ToString()
            },
            progress,
            cancellationToken);
    }

    public async Task<ContainerSample> ReadStatsAsync(
        DockerEndpoint endpoint,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        using var stream = await client.Containers.GetContainerStatsAsync(
            containerId,
            new ContainerStatsParameters { Stream = false },
            cancellationToken);
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(cancellationToken);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        var memory = root.GetProperty("memory_stats").GetProperty("usage").GetInt64();
        return new ContainerSample(CpuPercent(root), memory);
    }

    private static double CpuPercent(System.Text.Json.JsonElement root)
    {
        if (!root.TryGetProperty("cpu_stats", out var cpu) || !root.TryGetProperty("precpu_stats", out var previous))
        {
            return 0;
        }

        var cpuDelta = Number(cpu, "cpu_usage", "total_usage") - Number(previous, "cpu_usage", "total_usage");
        var systemDelta = Number(cpu, "system_cpu_usage") - Number(previous, "system_cpu_usage");
        if (cpuDelta <= 0 || systemDelta <= 0)
        {
            return 0;
        }

        var online = Number(cpu, "online_cpus");
        if (online <= 0
            && cpu.TryGetProperty("cpu_usage", out var usage)
            && usage.TryGetProperty("percpu_usage", out var perCpu)
            && perCpu.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            online = perCpu.GetArrayLength();
        }

        if (online <= 0)
        {
            online = 1;
        }

        return Math.Round(cpuDelta / systemDelta * online * 100.0, 2);
    }

    private static double Number(System.Text.Json.JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            if (!element.TryGetProperty(name, out element))
            {
                return 0;
            }
        }

        return element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetDouble(out var number)
            ? number
            : 0;
    }

    public async Task<string?> FindContainerIdByNameAsync(
        DockerEndpoint endpoint,
        string name,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [name] = true }
            }
        }, cancellationToken);
        return containers.FirstOrDefault(container => container.Names.Any(item => item == "/" + name || item == name))?.ID;
    }

    public async Task<string?> ReadHealthStatusAsync(
        DockerEndpoint endpoint,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var client = await ConnectAsync(endpoint, cancellationToken);
        var inspect = await client.Containers.InspectContainerAsync(containerId, cancellationToken);
        return inspect.State?.Health?.Status;
    }

    private static HealthcheckConfig? ToHealthcheck(ContainerHealthcheck? healthcheck)
    {
        if (healthcheck is null)
        {
            return null;
        }

        return new HealthcheckConfig
        {
            Test = healthcheck.Test.ToList(),
            Interval = healthcheck.Interval,
            Timeout = healthcheck.Timeout,
            StartPeriod = (long)healthcheck.StartPeriod.TotalMilliseconds * 1_000_000L,
            Retries = healthcheck.Retries
        };
    }

    private async Task<DockerClient> ConnectAsync(DockerEndpoint endpoint, CancellationToken cancellationToken)
    {
        var uri = EngineConnectUri.Parse(endpoint.Address);
        if (!uri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase))
        {
            return Client(uri, null);
        }

        var material = await ReadTcpMaterialAsync(endpoint.Address.Trim(), cancellationToken);
        if (material is null)
        {
            return Client(uri, null);
        }

        return Client(uri, new EngineClientCredentials(material));
    }

    private async Task<EngineCertificateMaterial?> ReadTcpMaterialAsync(string address, CancellationToken cancellationToken)
    {
        if (_scopes is null)
        {
            if (_allowCleartextTcp)
            {
                return null;
            }

            throw new DockerEngineException(EngineTls.CleartextMessage);
        }

        using var scope = _scopes.CreateScope();
        var hosts = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Hosts
            .AsNoTracking()
            .Where(host => host.Endpoint == address)
            .ToListAsync(cancellationToken);
        if (hosts.Count > 1)
        {
            throw new DockerEngineException(EngineTls.UnreadableMessage);
        }

        var host = hosts.SingleOrDefault();
        var cert = host?.ClientCertRef;
        var key = host?.ClientKeyRef;
        var ca = host?.CaRef;
        var any = !string.IsNullOrWhiteSpace(cert) || !string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(ca);
        var all = !string.IsNullOrWhiteSpace(cert) && !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(ca);
        if (!all)
        {
            if (_allowCleartextTcp && !any)
            {
                return null;
            }

            throw new DockerEngineException(EngineTls.CleartextMessage);
        }

        return await scope.ServiceProvider.GetRequiredService<IEngineCertificateLoader>()
            .LoadAsync(cert!, key!, ca!, cancellationToken);
    }

    private static DockerClient Client(Uri uri, Credentials? credentials) =>
        new DockerClientConfiguration(uri, credentials).CreateClient(new System.Version(1, 44), null!);

    private static string? AppSubnet(string name)
    {
        const string prefix = "cc-app-";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length != prefix.Length + 32)
        {
            return null;
        }

        var first = Convert.ToInt32(name.Substring(prefix.Length, 2), 16);
        var second = Convert.ToInt32(name.Substring(prefix.Length + 2, 2), 16);
        if (first == 0 && second == 0)
        {
            second = 1;
        }

        return $"10.{first}.{second}.0/24";
    }

    private static (string FromImage, string Tag) SplitImage(string image)
    {
        var slash = image.LastIndexOf('/');
        var colon = image.LastIndexOf(':');
        if (colon > slash)
        {
            return (image[..colon], image[(colon + 1)..]);
        }

        return (image, "latest");
    }

    private static RestartPolicyKind ToRestart(string policy) =>
        policy switch
        {
            "always" => RestartPolicyKind.Always,
            "on-failure" => RestartPolicyKind.OnFailure,
            "no" => RestartPolicyKind.No,
            _ => RestartPolicyKind.UnlessStopped
        };
}

public sealed class DockerEngineException : Exception
{
    public DockerEngineException(string message)
        : base(message)
    {
    }
}
