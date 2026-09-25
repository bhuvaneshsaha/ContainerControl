export interface PermissionLabel {
  code: string;
  displayName: string;
  module: string;
  description: string;
  opens?: string;
}

/** Mirrors Access PermissionCatalog display names so My access works without roles.manage. */
const catalog: readonly PermissionLabel[] = [
  { code: 'access.users.read', displayName: 'Read users', module: 'Access', description: 'View provisioned user accounts.' },
  { code: 'access.users.manage', displayName: 'Manage users', module: 'Access', description: 'Create and disable user accounts.', opens: 'Access' },
  { code: 'access.teams.manage', displayName: 'Manage teams', module: 'Access', description: 'Create teams and memberships.', opens: 'Access' },
  { code: 'access.roles.manage', displayName: 'Manage roles', module: 'Access', description: 'Compose roles from the permission catalog.', opens: 'Access' },
  { code: 'access.tokens.manage', displayName: 'Manage API tokens', module: 'Access', description: 'Issue CI API tokens.', opens: 'API tokens' },
  { code: 'access.breakglass.grant', displayName: 'Grant break-glass', module: 'Access', description: 'Grant a short-lived permission.', opens: 'Access' },
  { code: 'access.audit.read', displayName: 'Read audit', module: 'Access', description: 'Read the append-only audit log.', opens: 'Access' },
  { code: 'platform.hosts.manage', displayName: 'Manage Docker hosts', module: 'Platform', description: 'Register and update Docker hosts.', opens: 'Hosts' },
  { code: 'platform.quotas.manage', displayName: 'Manage quotas', module: 'Platform', description: 'Set team CPU, memory, and storage quotas.', opens: 'Capacity' },
  { code: 'platform.settings.manage', displayName: 'Manage platform settings', module: 'Platform', description: 'Change control-plane settings, including alert webhook and mail recipients.', opens: 'Alerts' },
  { code: 'platform.capacity.read', displayName: 'Read capacity', module: 'Platform', description: 'Read host capacity.', opens: 'Capacity' },
  { code: 'registries.read', displayName: 'Read registries', module: 'Registries', description: 'View registry connections.', opens: 'Registries' },
  { code: 'registries.manage', displayName: 'Manage registries', module: 'Registries', description: 'Create and update registry connections.', opens: 'Registries' },
  { code: 'apps.read', displayName: 'Read applications', module: 'Applications', description: 'View applications the caller may access.', opens: 'Applications' },
  { code: 'apps.write', displayName: 'Write applications', module: 'Applications', description: 'Create and update applications.', opens: 'Applications' },
  { code: 'secrets.read', displayName: 'Read secrets', module: 'Secrets', description: 'View secret names and injection mode.', opens: 'Applications' },
  { code: 'secrets.manage', displayName: 'Manage secrets', module: 'Secrets', description: 'Write secret values for non-production environments.', opens: 'Applications' },
  { code: 'secrets.manage.prod', displayName: 'Manage production secrets', module: 'Secrets', description: 'Write secret values for production.', opens: 'Applications' },
  { code: 'deploy.execute', displayName: 'Execute deploys', module: 'Delivery', description: 'Deploy, redeploy, start a slot beside the live release, swap traffic, or set a canary percent.', opens: 'Applications' },
  { code: 'deploy.approve', displayName: 'Approve deploys', module: 'Delivery', description: 'Accept a deploy that is waiting for approval.', opens: 'Applications' },
  { code: 'deploy.rollback', displayName: 'Roll back deploys', module: 'Delivery', description: 'Restore the last successful desired state, or send public traffic back to the previous slot.', opens: 'Applications' },
  { code: 'edge.certs.manage', displayName: 'Manage certificates', module: 'Edge', description: 'Upload and replace certificates.' },
  { code: 'edge.dns.manage', displayName: 'Manage allowed domains', module: 'Edge', description: 'Save domains that applications may claim.', opens: 'Allowed domains' },
  { code: 'runtime.logs.read', displayName: 'Read logs', module: 'Runtime', description: 'Read container logs, including lines kept after the Docker daemon rotates them.', opens: 'Applications' },
  { code: 'runtime.stats.read', displayName: 'Read stats', module: 'Runtime', description: 'Read CPU and memory stats.', opens: 'Applications' },
  { code: 'runtime.control', displayName: 'Control runtime', module: 'Runtime', description: 'Start, stop, and restart services.', opens: 'Applications' },
];

const byCode = new Map(catalog.map((item) => [item.code, item]));

export function describePermission(code: string): PermissionLabel {
  return (
    byCode.get(code) ?? {
      code,
      displayName: code,
      module: 'Other',
      description: '',
    }
  );
}

export interface PermissionGroup {
  module: string;
  items: PermissionLabel[];
}

export function groupPermissions(codes: readonly string[]): PermissionGroup[] {
  const groups: PermissionGroup[] = [];
  for (const code of codes) {
    const item = describePermission(code);
    const existing = groups.find((group) => group.module === item.module);
    if (existing) {
      existing.items.push(item);
    } else {
      groups.push({ module: item.module, items: [item] });
    }
  }
  return groups;
}
