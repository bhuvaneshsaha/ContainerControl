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
}

export interface AppListResponse {
  apps: AppResponse[];
}

export interface SecretResponse {
  id: string;
  name: string;
  environment: string;
  injectionMode: string;
  path: string;
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
