import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { CurrentUserPermissionsResponse } from '../../core/api-models';
import { PermissionService } from '../../core/permissions';

@Component({
  selector: 'app-permissions',
  imports: [],
  templateUrl: './permissions.html',
  styleUrl: './permissions.css',
})
export class Permissions {
  private readonly http = inject(HttpClient);
  private readonly permissionService = inject(PermissionService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly permissions = signal<readonly string[]>([]);

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const response = await firstValueFrom(
        this.http.get<CurrentUserPermissionsResponse>(`${environment.apiUrl}/me/permissions`),
      );
      this.permissionService.setPermissions(response.permissions);
      this.permissions.set(response.permissions);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }
}
