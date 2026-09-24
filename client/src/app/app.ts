import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth';
import { HasPermission } from './shared/has-permission';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, HasPermission],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly signedIn = this.auth.signedIn;

  constructor() {
    void this.auth.restore();
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
    await this.router.navigateByUrl('/sign-in');
  }
}
