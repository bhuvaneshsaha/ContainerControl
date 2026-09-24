import { describePermission } from './permission-labels';

export interface HomeCandidate {
  path: string;
  label: string;
  anyOf: readonly string[];
}

/** Same order as the primary nav, without My access. First match is the post-auth home. */
export const homeCandidates: readonly HomeCandidate[] = [
  { path: '/apps', label: 'Applications', anyOf: ['apps.read'] },
  {
    path: '/access',
    label: 'Access',
    anyOf: ['access.users.manage', 'access.teams.manage', 'access.roles.manage', 'access.audit.read', 'access.breakglass.grant'],
  },
  { path: '/hosts', label: 'Hosts', anyOf: ['platform.hosts.manage'] },
  { path: '/capacity', label: 'Capacity', anyOf: ['platform.quotas.manage', 'platform.capacity.read'] },
  { path: '/registries', label: 'Registries', anyOf: ['registries.read'] },
  { path: '/domains', label: 'Allowed domains', anyOf: ['edge.dns.manage'] },
  { path: '/tokens', label: 'API tokens', anyOf: ['access.tokens.manage'] },
];

export function resolveHomePath(hasPermission: (code: string) => boolean): string {
  return homeCandidates.find((item) => item.anyOf.some((code) => hasPermission(code)))?.path ?? '/permissions';
}

export function homeLabel(path: string): string {
  return homeCandidates.find((item) => item.path === path)?.label ?? 'My access';
}

export function denialMessage(required: readonly string[]): string {
  const parts = required.map((code) => `${describePermission(code).displayName} (${code})`);
  if (parts.length === 1) {
    return `You need ${parts[0]} to open that page.`;
  }

  return `You need one of these permissions to open that page: ${parts.join(', ')}.`;
}
