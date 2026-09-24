using ContainerControl.Modules.Platform.Engine;

namespace ContainerControl.Modules.Delivery.Runs;

public static class HealthcheckGate
{
    public static readonly TimeSpan MaximumWait = TimeSpan.FromMinutes(5);

    public static TimeSpan Budget(ContainerHealthcheck healthcheck)
    {
        var attempts = Math.Max(healthcheck.Retries, 1);
        var oneTry = healthcheck.Interval + healthcheck.Timeout;
        var budget = healthcheck.StartPeriod + TimeSpan.FromTicks(oneTry.Ticks * attempts);
        if (budget < TimeSpan.FromSeconds(1))
        {
            budget = TimeSpan.FromSeconds(1);
        }

        return budget > MaximumWait ? MaximumWait : budget;
    }

    public static async Task WaitAsync(
        Func<CancellationToken, Task<string?>> readStatus,
        TimeSpan budget,
        TimeSpan poll,
        CancellationToken cancellationToken)
    {
        if (poll <= TimeSpan.Zero)
        {
            poll = TimeSpan.FromSeconds(1);
        }

        var polls = Math.Max(1, (int)Math.Ceiling(budget.TotalMilliseconds / poll.TotalMilliseconds));
        for (var attempt = 0; attempt < polls; attempt++)
        {
            var status = await readStatus(cancellationToken);
            if (string.Equals(status, "healthy", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(status, "unhealthy", StringComparison.OrdinalIgnoreCase))
            {
                throw new DockerEngineException("A service healthcheck failed.");
            }

            if (attempt < polls - 1)
            {
                await Task.Delay(poll, cancellationToken);
            }
        }

        throw new DockerEngineException("A service healthcheck did not become healthy.");
    }
}
