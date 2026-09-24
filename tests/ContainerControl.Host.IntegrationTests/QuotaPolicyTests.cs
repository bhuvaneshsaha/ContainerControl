using ContainerControl.Modules.Delivery.Compose;
using ContainerControl.Modules.Platform.Quotas;

namespace ContainerControl.Host.IntegrationTests;

public class QuotaPolicyTests
{
    [Fact]
    public void A_team_without_a_quota_can_deploy_services_that_omit_limits()
    {
        Assert.Null(QuotaPolicy.Rejection(null, [null]));
    }

    [Fact]
    public void A_quota_rejects_a_service_that_does_not_declare_limits()
    {
        var quota = new ServiceResources(1000, 1024, 1024);
        Assert.Equal("Each service needs a CPU, memory, and storage limit.", QuotaPolicy.Rejection(quota, [null]));
    }

    [Fact]
    public void A_quota_rejects_the_resource_that_is_over_the_team_allowance()
    {
        var quota = new ServiceResources(1000, 2048, 4096);
        Assert.Equal(
            "The services exceed the team CPU quota.",
            QuotaPolicy.Rejection(quota, [new ServiceResources(1500, 1024, 1024)]));
        Assert.Equal(
            "The services exceed the team memory quota.",
            QuotaPolicy.Rejection(quota, [new ServiceResources(500, 4096, 1024)]));
        Assert.Equal(
            "The services exceed the team storage quota.",
            QuotaPolicy.Rejection(quota, [new ServiceResources(500, 1024, 8192)]));
    }

    [Fact]
    public void Compose_limits_become_millicores_and_bytes()
    {
        var plan = ComposePolicy.Parse("""
            services:
              web:
                image: nginx:1.27
                deploy:
                  resources:
                    limits:
                      cpus: "0.5"
                      memory: 256M
                      storage: 1G
            """);

        Assert.True(plan.Accepted);
        var resources = Assert.Single(plan.Services).Resources;
        Assert.NotNull(resources);
        Assert.Equal(500, resources.CpuMillicores);
        Assert.Equal(256L * 1024 * 1024, resources.MemoryBytes);
        Assert.Equal(1024L * 1024 * 1024, resources.StorageBytes);
    }

    [Fact]
    public void Data_space_total_is_the_storage_capacity()
    {
        var bytes = HostCapacityText.DataSpaceBytes([["Data Space Used", "1GB"], ["Data Space Total", "10GB"]]);
        Assert.Equal(10L * 1024 * 1024 * 1024, bytes);
        Assert.Null(HostCapacityText.DataSpaceBytes([["Backing Filesystem", "extfs"]]));
    }
}
