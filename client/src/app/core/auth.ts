import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
import { CurrentUserPermissionsResponse } from './api-models';
import { PermissionService } from './permissions';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly permissions = inject(PermissionService);

  readonly signedIn = signal(false);

  async restore(): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.get<CurrentUserPermissionsResponse>(`${environment.apiUrl}/me/permissions`),
      );
      this.permissions.setPermissions(response.permissions);
      this.signedIn.set(true);
      return true;
    } catch {
      this.permissions.setPermissions([]);
      this.signedIn.set(false);
      return false;
    }
  }

  async signIn(email: string, password: string): Promise<void> {
    await firstValueFrom(this.http.get(`${environment.apiUrl}/auth/csrf`));
    await firstValueFrom(
      this.http.post(`${environment.apiUrl}/auth/login`, {
        email,
        password,
      }),
    );
    const restored = await this.restore();
    if (!restored) {
      throw new Error('Sign-in failed. Check the email and password.');
    }
  }

  async signOut(): Promise<void> {
    await firstValueFrom(this.http.post(`${environment.apiUrl}/auth/logout`, {}));
    this.permissions.setPermissions([]);
    this.signedIn.set(false);
  }
}
