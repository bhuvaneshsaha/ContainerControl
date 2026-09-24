using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Delivery.Runs;
using ContainerControl.Modules.Platform.Engine;

namespace ContainerControl.Host.IntegrationTests;

public class HealthcheckTests
{
    [Fact]
    public void Compose_healthcheck_is_parsed_and_dependencies_start_first()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: busybox:1.36.1
                depends_on:
                  - cache
              cache:
                image: busybox:1.36.1
                healthcheck:
                  test: ["CMD", "true"]
                  interval: 1s
                  timeout: 2s
                  retries: 4
                  start_period: 3s
            """);

        Assert.True(plan.Accepted);
        var ordered = ComposePolicy.StartOrder(plan.Services);
        Assert.Equal(["cache", "web"], ordered.Select(service => service.Name).ToArray());
        var health = ordered[0].Healthcheck;
        Assert.NotNull(health);
        Assert.Equal(["CMD", "true"], health!.Test);
        Assert.Equal(TimeSpan.FromSeconds(1), health.Interval);
        Assert.Equal(TimeSpan.FromSeconds(2), health.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(3), health.StartPeriod);
        Assert.Equal(4, health.Retries);
        Assert.Null(ordered[1].Healthcheck);
    }

    [Fact]
    public void String_healthcheck_becomes_a_shell_test_and_disable_is_ignored()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: busybox:1.36.1
                healthcheck:
                  test: "true"
              skip:
                image: busybox:1.36.1
                healthcheck:
                  disable: true
                  test: ["CMD", "false"]
            """);

        Assert.True(plan.Accepted);
        Assert.Equal(["CMD-SHELL", "true"], plan.Services[0].Healthcheck!.Test);
        Assert.Null(plan.Services[1].Healthcheck);
    }

    [Fact]
    public void A_cycle_or_missing_dependency_is_rejected_before_any_service_is_kept()
    {
        var cycle = ComposePolicy.Parse("""
            services:
              web:
                image: busybox:1.36.1
                depends_on: [cache]
              cache:
                image: busybox:1.36.1
                depends_on: [web]
            """);
        Assert.False(cycle.Accepted);
        Assert.Contains(cycle.Errors, error => error.Contains("cycle", StringComparison.Ordinal));
        Assert.Empty(cycle.Services);

        var missing = ComposePolicy.Parse("""
            services:
              web:
                image: busybox:1.36.1
                depends_on: [cache]
            """);
        Assert.False(missing.Accepted);
        Assert.Contains(missing.Errors, error => error.Contains("not in the file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Wait_continues_only_after_healthy_and_stops_on_unhealthy()
    {
        var reads = 0;
        await HealthcheckGate.WaitAsync(
            _ => Task.FromResult<string?>(++reads < 3 ? "starting" : "healthy"),
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None);

        var error = await Assert.ThrowsAsync<DockerEngineException>(() => HealthcheckGate.WaitAsync(
            _ => Task.FromResult<string?>("unhealthy"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None));
        Assert.Equal("A service healthcheck failed.", error.Message);
    }

    [Fact]
    public async Task Wait_fails_when_the_budget_ends_still_starting()
    {
        var error = await Assert.ThrowsAsync<DockerEngineException>(() => HealthcheckGate.WaitAsync(
            _ => Task.FromResult<string?>("starting"),
            TimeSpan.FromMilliseconds(25),
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None));
        Assert.Equal("A service healthcheck did not become healthy.", error.Message);
    }

    [Fact]
    public void Budget_includes_start_period_and_is_capped()
    {
        var budget = HealthcheckGate.Budget(new ContainerHealthcheck(
            ["CMD", "true"],
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            3));
        Assert.Equal(TimeSpan.FromSeconds(8), budget);

        var capped = HealthcheckGate.Budget(new ContainerHealthcheck(
            ["CMD", "true"],
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(2),
            10));
        Assert.Equal(HealthcheckGate.MaximumWait, capped);
    }
}
