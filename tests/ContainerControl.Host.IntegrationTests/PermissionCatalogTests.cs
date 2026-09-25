using ContainerControl.Modules.Access.Domain.Permissions;
using ContainerControl.Modules.Platform.Http;

namespace ContainerControl.Host.IntegrationTests;

public class PermissionCatalogTests
{
    [Fact]
    public void Catalog_contains_the_platform_plan_codes()
    {
        string[] expected =
        [
            "access.users.read",
            "access.users.manage",
            "access.teams.manage",
            "access.roles.manage",
            "access.tokens.manage",
            "access.breakglass.grant",
            "access.audit.read",
            "platform.hosts.manage",
            "platform.quotas.manage",
            "platform.settings.manage",
            "platform.capacity.read",
            "registries.read",
            "registries.manage",
            "apps.read",
            "apps.write",
            "apps.templates.manage",
            "secrets.read",
            "secrets.manage",
            "secrets.manage.prod",
            "deploy.execute",
            "deploy.approve",
            "deploy.rollback",
            "edge.certs.manage",
            "edge.dns.manage",
            "runtime.logs.read",
            "runtime.stats.read",
            "runtime.control"
        ];

        var actual = PermissionCatalog.All.Select(permission => permission.Code).ToArray();
        Assert.Equal(expected.OrderBy(code => code), actual.OrderBy(code => code));
        Assert.Equal(PermissionCatalog.PlatformHostsManage, PlatformPermissions.HostsManage);
        Assert.Equal(PermissionCatalog.PlatformQuotasManage, PlatformPermissions.QuotasManage);
        Assert.Equal(PermissionCatalog.PlatformCapacityRead, PlatformPermissions.CapacityRead);
    }
}
