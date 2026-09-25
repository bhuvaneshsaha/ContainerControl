namespace ContainerControl.Modules.Access.Domain.Permissions;

/// <summary>
/// Product-owned permission codes. Operators compose roles from this catalog.
/// Adding a code is a product change. Creating a role is data.
/// </summary>
public static class PermissionCatalog
{
    public const string AccessUsersRead = "access.users.read";
    public const string AccessUsersManage = "access.users.manage";
    public const string AccessTeamsManage = "access.teams.manage";
    public const string AccessRolesManage = "access.roles.manage";
    public const string AccessTokensManage = "access.tokens.manage";
    public const string AccessBreakGlassGrant = "access.breakglass.grant";
    public const string AccessAuditRead = "access.audit.read";

    public const string PlatformHostsManage = "platform.hosts.manage";
    public const string PlatformQuotasManage = "platform.quotas.manage";
    public const string PlatformSettingsManage = "platform.settings.manage";
    public const string PlatformCapacityRead = "platform.capacity.read";

    public const string RegistriesRead = "registries.read";
    public const string RegistriesManage = "registries.manage";

    public const string AppsRead = "apps.read";
    public const string AppsWrite = "apps.write";
    public const string AppsTemplatesManage = "apps.templates.manage";

    public const string SecretsRead = "secrets.read";
    public const string SecretsManage = "secrets.manage";
    public const string SecretsManageProd = "secrets.manage.prod";

    public const string DeployExecute = "deploy.execute";
    public const string DeployApprove = "deploy.approve";
    public const string DeployRollback = "deploy.rollback";

    public const string EdgeCertsManage = "edge.certs.manage";
    public const string EdgeDnsManage = "edge.dns.manage";

    public const string RuntimeLogsRead = "runtime.logs.read";
    public const string RuntimeStatsRead = "runtime.stats.read";
    public const string RuntimeControl = "runtime.control";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(AccessUsersRead, "Read users", "Access", "View provisioned user accounts."),
        new(AccessUsersManage, "Manage users", "Access", "Create and disable user accounts."),
        new(AccessTeamsManage, "Manage teams", "Access", "Create teams and memberships."),
        new(AccessRolesManage, "Manage roles", "Access", "Compose roles from the permission catalog."),
        new(AccessTokensManage, "Manage API tokens", "Access", "Issue and revoke CI API tokens."),
        new(AccessBreakGlassGrant, "Grant break-glass", "Access", "Grant a short-lived permission."),
        new(AccessAuditRead, "Read audit", "Access", "Read the append-only audit log."),

        new(PlatformHostsManage, "Manage Docker hosts", "Platform", "Register and update Docker hosts."),
        new(PlatformQuotasManage, "Manage quotas", "Platform", "Set team CPU, memory, and storage quotas."),
        new(PlatformSettingsManage, "Manage platform settings", "Platform", "Change control-plane settings."),
        new(PlatformCapacityRead, "Read capacity", "Platform", "Read host capacity."),

        new(RegistriesRead, "Read registries", "Registries", "View registry connections."),
        new(RegistriesManage, "Manage registries", "Registries", "Create and update registry connections."),

        new(AppsRead, "Read applications", "Applications", "View applications the caller may access."),
        new(AppsWrite, "Write applications", "Applications", "Create and update applications."),
        new(AppsTemplatesManage, "Manage application templates", "Applications", "Publish and remove application templates."),

        new(SecretsRead, "Read secrets", "Secrets", "View secret names and injection mode."),
        new(SecretsManage, "Manage secrets", "Secrets", "Write secret values for non-production environments."),
        new(SecretsManageProd, "Manage production secrets", "Secrets", "Write secret values for production."),

        new(DeployExecute, "Execute deploys", "Delivery", "Deploy and redeploy an application."),
        new(DeployApprove, "Approve deploys", "Delivery", "Accept a deploy that is waiting for approval."),
        new(DeployRollback, "Roll back deploys", "Delivery", "Restore the last successful desired state."),

        new(EdgeCertsManage, "Manage certificates", "Edge", "Upload and replace certificates."),
        new(EdgeDnsManage, "Manage allowed domains", "Edge", "Save domains that applications may claim."),

        new(RuntimeLogsRead, "Read logs", "Runtime", "Read container logs."),
        new(RuntimeStatsRead, "Read stats", "Runtime", "Read CPU and memory stats."),
        new(RuntimeControl, "Control runtime", "Runtime", "Start, stop, and restart services.")
    ];

    public static bool Contains(string code) =>
        All.Any(permission => string.Equals(permission.Code, code, StringComparison.Ordinal));
}
