using Docker.DotNet;
using Docker.DotNet.Models;

namespace ContainerControl.Modules.Platform.Engine;

public sealed class DockerEngineClient : IDockerEngine
{
    public async Task<EngineVersion> GetVersionAsync(DockerEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
        var version = await client.System.GetVersionAsync(cancellationToken);
        return new EngineVersion(version.Version, version.APIVersion);
    }

    public async Task<IReadOnlyList<string>> ListContainerIdsAsync(DockerEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken);
        return containers.Select(container => container.ID).ToArray();
    }

    public async Task EnsureNetworkAsync(DockerEndpoint endpoint, string name, CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
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
        using var client = Connect(endpoint);
        try
        {
            await client.Networks.DeleteNetworkAsync(name, cancellationToken);
        }
        catch (DockerApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }
    }

    public async Task PullImageAsync(DockerEndpoint endpoint, string image, CancellationToken cancellationToken)
    {
        var (fromImage, tag) = SplitImage(image);
        using var client = Connect(endpoint);
        await client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = fromImage, Tag = tag },
            null,
            new Progress<JSONMessage>(),
            cancellationToken);
    }

    public async Task<string> CreateContainerAsync(DockerEndpoint endpoint, ContainerPlan plan, CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
        var labels = new Dictionary<string, string>(plan.Labels, StringComparer.Ordinal);
        var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = plan.Name,
            Image = plan.Image,
            Cmd = plan.Command?.ToList(),
            Env = plan.Environment.Select(pair => pair.Key + "=" + pair.Value).ToList(),
            Labels = labels,
            HostConfig = new HostConfig
            {
                Binds = plan.Binds.ToList(),
                PortBindings = plan.PublishedPorts.ToDictionary(
                    pair => pair.Key,
                    pair => (IList<PortBinding>)new List<PortBinding> { new() { HostPort = pair.Value } }),
                RestartPolicy = new RestartPolicy { Name = ToRestart(plan.RestartPolicy) }
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
        using var client = Connect(endpoint);
        await client.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new ContainerPathStatParameters { Path = destinationPath },
            archive,
            cancellationToken);
    }

    public async Task StartContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
        await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);
    }

    public async Task StopContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
        await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 5 }, cancellationToken);
    }

    public async Task RemoveContainerAsync(DockerEndpoint endpoint, string containerId, CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
        await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<EngineContainer>> ListByLabelAsync(
        DockerEndpoint endpoint,
        string label,
        CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
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
            return new EngineContainer(container.ID, container.Names.FirstOrDefault() ?? container.ID, container.State == "running", ip);
        }).ToArray();
    }

    public async Task<string> ExecAsync(
        DockerEndpoint endpoint,
        string containerId,
        IReadOnlyList<string> command,
        CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
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
        using var client = Connect(endpoint);
        using var stream = await client.Containers.GetContainerLogsAsync(containerId, new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Tail = tail.ToString()
        }, cancellationToken);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
        return stdout + stderr;
    }

    public async Task<ContainerSample> ReadStatsAsync(
        DockerEndpoint endpoint,
        string containerId,
        CancellationToken cancellationToken)
    {
        using var client = Connect(endpoint);
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
        using var client = Connect(endpoint);
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

    private static DockerClient Connect(DockerEndpoint endpoint)
    {
        if (!Uri.TryCreate(endpoint.Address, UriKind.Absolute, out var uri))
        {
            throw new DockerEngineException("The Docker host endpoint is not a valid URI.");
        }

        return new DockerClientConfiguration(uri).CreateClient(new System.Version(1, 44), null!);
    }

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
