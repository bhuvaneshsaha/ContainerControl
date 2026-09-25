import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth';
import { ConfirmService } from './core/confirm';
import { FeedbackService } from './core/feedback';
import { PermissionService } from './core/permissions';
import { ConfirmDialog } from './shared/confirm-dialog';
import { FeedbackBanner } from './shared/feedback-banner';
import { HasPermission } from './shared/has-permission';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, HasPermission, FeedbackBanner, ConfirmDialog, MatButtonModule],
  templateUrl: './app.html',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  private readonly router = inject(Router);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);

  readonly signedIn = this.auth.signedIn;

  canOpenTemplates(): boolean {
    return ['apps.read', 'apps.write', 'apps.templates.manage'].some((code) => this.permissions.hasPermission(code));
  }

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
    this.feedback.clear();
    if (this.confirm.request()) {
      this.confirm.answer(false);
    }
    await this.auth.signOut();
    await this.router.navigateByUrl('/sign-in');
  }
}
