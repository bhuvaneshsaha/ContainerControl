using System.Globalization;

namespace ContainerControl.Modules.Platform.Quotas;

public sealed record ServiceResources(long CpuMillicores, long MemoryBytes, long StorageBytes);

public static class ResourceQuantity
{
    public static bool TryParseCpus(string? text, out long millicores)
    {
        millicores = 0;
        if (string.IsNullOrWhiteSpace(text)
            || !decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var cpus)
            || cpus <= 0
            || cpus > 1_000_000)
        {
            return false;
        }

        millicores = (long)decimal.Round(cpus * 1000m, 0, MidpointRounding.AwayFromZero);
        return millicores > 0;
    }

    public static bool TryParseBytes(string? text, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        var index = 0;
        while (index < value.Length && (char.IsDigit(value[index]) || value[index] == '.'))
        {
            index++;
        }

        if (index == 0
            || !decimal.TryParse(value[..index], NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            || number <= 0
            || !Multiplier(value[index..].Trim(), out var multiplier))
        {
            return false;
        }

        var scaled = number * multiplier;
        if (scaled > long.MaxValue)
        {
            return false;
        }

        bytes = (long)decimal.Round(scaled, 0, MidpointRounding.AwayFromZero);
        return bytes > 0;
    }

    private static bool Multiplier(string unit, out decimal multiplier)
    {
        multiplier = unit.ToLowerInvariant() switch
        {
            "" or "b" or "byte" or "bytes" => 1m,
            "k" or "kb" or "ki" or "kib" => 1024m,
            "m" or "mb" or "mi" or "mib" => 1024m * 1024,
            "g" or "gb" or "gi" or "gib" => 1024m * 1024 * 1024,
            "t" or "tb" or "ti" or "tib" => 1024m * 1024 * 1024 * 1024,
            _ => 0m
        };
        return multiplier > 0;
    }
}

public static class QuotaPolicy
{
    public static string? Rejection(ServiceResources? quota, IReadOnlyList<ServiceResources?> services)
    {
        if (quota is null)
        {
            return null;
        }

        if (services.Count == 0 || services.Any(service => service is null))
        {
            return "Each service needs a CPU, memory, and storage limit.";
        }

        if (services.Sum(service => service!.CpuMillicores) > quota.CpuMillicores)
        {
            return "The services exceed the team CPU quota.";
        }

        if (services.Sum(service => service!.MemoryBytes) > quota.MemoryBytes)
        {
            return "The services exceed the team memory quota.";
        }

        if (services.Sum(service => service!.StorageBytes) > quota.StorageBytes)
        {
            return "The services exceed the team storage quota.";
        }

        return null;
    }
}

public static class HostCapacityText
{
    public static long? DataSpaceBytes(IEnumerable<string[]>? rows)
    {
        if (rows is null)
        {
            return null;
        }

        foreach (var row in rows)
        {
            if (row.Length >= 2
                && row[0].Trim().Equals("Data Space Total", StringComparison.OrdinalIgnoreCase)
                && ResourceQuantity.TryParseBytes(row[1], out var bytes))
            {
                return bytes;
            }
        }

        return null;
    }
}
