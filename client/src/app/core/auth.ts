import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
import { SessionResponse } from './api-models';
import { PermissionService } from './permissions';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly permissions = inject(PermissionService);

  readonly signedIn = signal(false);
  private sessionRequest: Promise<boolean> | null = null;

  restore(): Promise<boolean> {
    if (this.signedIn()) {
      return Promise.resolve(true);
    }

    this.sessionRequest ??= this.loadSession().finally(() => {
      this.sessionRequest = null;
    });
    return this.sessionRequest;
  }

  async signIn(email: string, password: string): Promise<void> {
    await firstValueFrom(this.http.get(`${environment.apiUrl}/auth/csrf`));
    await firstValueFrom(
      this.http.post(`${environment.apiUrl}/auth/login`, {
        email,
        password,
      }),
    );
    const restored = await this.loadSession();
    if (!restored) {
      throw new Error('Sign-in failed. Check the email and password.');
    }
  }

  async signOut(): Promise<void> {
    await firstValueFrom(this.http.get(`${environment.apiUrl}/auth/csrf`));
    await firstValueFrom(this.http.post(`${environment.apiUrl}/auth/logout`, {}));
    this.permissions.setPermissions([]);
    this.signedIn.set(false);
  }

  private async loadSession(): Promise<boolean> {
    try {
      const response = await firstValueFrom(
        this.http.get<SessionResponse>(`${environment.apiUrl}/auth/session`),
      );
      if (!response.signedIn) {
        this.permissions.setPermissions([]);
        this.signedIn.set(false);
        return false;
      }

      this.permissions.setPermissions(response.permissions);
      this.signedIn.set(true);
      return true;
    } catch {
      this.permissions.setPermissions([]);
      this.signedIn.set(false);
      return false;
    }
  }
}
