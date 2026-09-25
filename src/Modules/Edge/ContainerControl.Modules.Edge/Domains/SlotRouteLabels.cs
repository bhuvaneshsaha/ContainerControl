using System.Text;
using ContainerControl.SharedKernel.Hostnames;

namespace ContainerControl.Modules.Edge.Domains;

public sealed record SlotWeight(string Slot, string ServiceName, int Weight);

/// <summary>
/// Traefik publishes the hostname. The Docker provider does not accept a weighted
/// service, so a route container runs nginx and applies the percent. A weight of
/// zero is omitted. The route router uses a higher priority than a router left
/// on a classic one-replica container.
/// </summary>
public static class SlotRouteLabels
{
    public const string ProxyEnvironment = "CC_SLOT_PROXY";

    public static IReadOnlyList<string> ProxyCommand { get; } =
    [
        "sh",
        "-c",
        "printf '%s' \"$" + ProxyEnvironment + "\" > /etc/nginx/nginx.conf && exec nginx -g 'daemon off;'"
    ];

    public static IReadOnlyDictionary<string, string> Backend(string serviceName, int port, string edgeNetwork) =>
        new Dictionary<string, string>
        {
            ["traefik.enable"] = "true",
            ["traefik.docker.network"] = edgeNetwork,
            ["traefik.http.services." + serviceName + ".loadbalancer.server.port"] = port.ToString()
        };

    public static IReadOnlyDictionary<string, string> Director(
        string routerName,
        string hostname,
        bool tls,
        string edgeNetwork)
    {
        var rule = PublicHostname.TraefikHostRule(hostname);
        if (rule is null)
        {
            throw new ArgumentException("The hostname is not a DNS name.", nameof(hostname));
        }

        var router = "traefik.http.routers." + routerName + "slot";
        var service = routerName + "slotlb";
        var labels = new Dictionary<string, string>
        {
            ["traefik.enable"] = "true",
            ["traefik.docker.network"] = edgeNetwork,
            [router + ".rule"] = rule,
            [router + ".entrypoints"] = tls ? "websecure" : "web",
            [router + ".priority"] = "200",
            [router + ".service"] = service,
            ["traefik.http.services." + service + ".loadbalancer.server.port"] = "80"
        };
        if (tls)
        {
            labels[router + ".tls"] = "true";
            labels[router + ".tls.certresolver"] = "le";
        }

        return labels;
    }

    public static string ProxyConfig(IReadOnlyList<(string Host, int Port, int Weight)> upstreams)
    {
        var servers = upstreams.Where(item => item.Weight > 0).ToArray();
        if (servers.Length == 0)
        {
            throw new ArgumentException("At least one slot needs traffic.", nameof(upstreams));
        }

        var config = new StringBuilder();
        config.AppendLine("worker_processes 1;");
        config.AppendLine("error_log /var/log/nginx/error.log warn;");
        config.AppendLine("pid /var/run/nginx.pid;");
        config.AppendLine("events { worker_connections 256; }");
        config.AppendLine("http {");
        config.AppendLine("  include /etc/nginx/mime.types;");
        config.AppendLine("  default_type application/octet-stream;");
        config.AppendLine("  access_log off;");
        config.AppendLine("  upstream slot {");
        foreach (var server in servers)
        {
            if (server.Port is < 1 or > 65535 || !IsDnsName(server.Host))
            {
                throw new ArgumentException("The upstream address is not a container DNS name.", nameof(upstreams));
            }

            config.Append("    server ").Append(server.Host).Append(':').Append(server.Port)
                .Append(" weight=").Append(server.Weight).AppendLine(";");
        }

        config.AppendLine("  }");
        config.AppendLine("  server {");
        config.AppendLine("    listen 80;");
        config.AppendLine("    location / {");
        config.AppendLine("      proxy_pass http://slot;");
        config.AppendLine("      proxy_http_version 1.1;");
        config.AppendLine("      proxy_set_header Host $host;");
        config.AppendLine("      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
        config.AppendLine("      proxy_set_header X-Forwarded-Proto $scheme;");
        config.AppendLine("    }");
        config.AppendLine("  }");
        config.AppendLine("}");
        return config.ToString();
    }

    private static bool IsDnsName(string host) =>
        host.Length > 0 && host.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '.');
}
