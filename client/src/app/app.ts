import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth';
import { PermissionService } from './core/permissions';
import { HasPermission } from './shared/has-permission';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, HasPermission],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  private readonly router = inject(Router);

  readonly signedIn = this.auth.signedIn;

  canOpenCapacity(): boolean {
    return this.permissions.hasPermission('platform.quotas.manage') || this.permissions.hasPermission('platform.capacity.read');
  }

  canOpenAccess(): boolean {
    return ['access.users.manage', 'access.teams.manage', 'access.roles.manage', 'access.audit.read', 'access.breakglass.grant'].some((code) =>
      this.permissions.hasPermission(code),
    );
  }

  constructor() {
    void this.auth.restore();
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/sign-in');
  }
}
