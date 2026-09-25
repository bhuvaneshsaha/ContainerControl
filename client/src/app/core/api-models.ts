export interface CurrentUserPermissionsResponse {
  permissions: string[];
}

export interface SessionResponse {
  signedIn: boolean;
  permissions: string[];
}

export interface HostSummary {
  id: string;
  name: string;
  endpoint?: string;
  engineVersion?: string | null;
  lastPingAtUtc?: string | null;
  clientCertRef?: string | null;
  clientKeyRef?: string | null;
  caRef?: string | null;
}

export interface HostListResponse {
  hosts: HostSummary[];
}

export interface TeamResponse {
  id: string;
  name: string;
}

export interface TeamListResponse {
  teams: TeamResponse[];
}

export interface UserSummary {
  id: string;
  email: string;
  displayName: string;
  disabled: boolean;
}

export interface UserListResponse {
  users: UserSummary[];
}

export interface RoleSummary {
  id: string;
  name: string;
  description?: string | null;
  permissionCodes: string[];
}

export interface PermissionCatalogItem {
  code: string;
  displayName: string;
  module: string;
  description: string;
}

export interface PermissionCatalogResponse {
  permissions: PermissionCatalogItem[];
}

export interface AuditEntry {
  id: string;
  occurredAtUtc: string;
  actorUserId?: string | null;
  action: string;
  subjectType: string;
  subjectId?: string | null;
}

export interface AuditListResponse {
  entries: AuditEntry[];
}

export interface RoleListResponse {
  roles: RoleSummary[];
}

export interface AppResponse {
  id: string;
  teamId: string;
  hostId: string;
  name: string;
  environment: string;
  image: string | null;
  status: string;
  hostname: string | null;
  exposed: boolean;
  requiresApproval: boolean;
  allowDatabaseImages: boolean;
  composeYaml?: string | null;
  internalPort?: number | null;
  command?: string[] | null;
}

export interface AppListResponse {
  apps: AppResponse[];
}

export interface TemplateResponse {
  id: string;
  name: string;
  description: string;
  composeYaml: string;
}

export interface TemplateListResponse {
  templates: TemplateResponse[];
}

export interface SecretResponse {
  id: string;
  name: string;
  environment: string;
  injectionMode: string;
  path: string;
  serviceNames: string[];
}

export interface SecretListResponse {
  secrets: SecretResponse[];
}

export interface DomainResponse {
  id: string;
  name: string;
}

export interface DomainListResponse {
  domains: DomainResponse[];
}
