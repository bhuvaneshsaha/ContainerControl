export interface CurrentUserPermissionsResponse {
  permissions: string[];
}

export interface HostSummary {
  id: string;
  name: string;
}

export interface HostListResponse {
  hosts: HostSummary[];
}
