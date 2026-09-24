import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { RoleListResponse, TeamListResponse, UserListResponse, UserSummary } from '../../core/api-models';

@Component({
  selector: 'app-access',
  imports: [ReactiveFormsModule],
  templateUrl: './access.html',
})
export class Access {
  private readonly http = inject(HttpClient);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
  readonly users = signal<readonly UserSummary[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly roles = signal<{ id: string; name: string }[]>([]);
  readonly userForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    roleId: new FormControl('', { nonNullable: true }),
  });
  readonly teamForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
  readonly memberForm = new FormGroup({
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    userId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const [users, teams] = await Promise.all([
        firstValueFrom(this.http.get<UserListResponse>(`${environment.apiUrl}/access/users`)),
        firstValueFrom(this.http.get<TeamListResponse>(`${environment.apiUrl}/access/teams`)),
      ]);
      this.users.set(users.users);
      this.teams.set(teams.teams);
      try {
        const roles = await firstValueFrom(this.http.get<RoleListResponse>(`${environment.apiUrl}/access/roles`));
        this.roles.set(roles.roles);
      } catch {
        this.roles.set([]);
      }
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  async createUser(): Promise<void> {
    this.message.set('');
    if (this.userForm.invalid) {
      this.message.set('Enter an email, a display name, and a password.');
      return;
    }

    const value = this.userForm.getRawValue();
    try {
      const created = await firstValueFrom(
        this.http.post<{ id: string }>(`${environment.apiUrl}/access/users`, {
          email: value.email.trim(),
          displayName: value.displayName.trim(),
          password: value.password,
        }),
      );
      if (value.roleId) {
        await firstValueFrom(
          this.http.put(`${environment.apiUrl}/access/users/${created.id}/roles`, { roleIds: [value.roleId] }),
        );
      }
      this.userForm.reset({ email: '', displayName: '', password: '', roleId: '' });
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The user could not be created.'));
    }
  }

  async disable(user: UserSummary): Promise<void> {
    this.message.set('');
    try {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/access/users/${user.id}/disable`, {}));
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The user could not be disabled.'));
    }
  }

  async createTeam(): Promise<void> {
    this.message.set('');
    if (this.teamForm.invalid) {
      this.message.set('Enter a team name.');
      return;
    }

    try {
      await firstValueFrom(
        this.http.post(`${environment.apiUrl}/access/teams`, { name: this.teamForm.controls.name.value.trim() }),
      );
      this.teamForm.reset({ name: '' });
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The team could not be created.'));
    }
  }

  async addMember(): Promise<void> {
    this.message.set('');
    if (this.memberForm.invalid) {
      this.message.set('Select a team and a user.');
      return;
    }

    const value = this.memberForm.getRawValue();
    try {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/access/teams/${value.teamId}/members`, { userId: value.userId }));
      this.message.set('The user was added to the team.');
    } catch (error) {
      this.message.set(problemMessage(error, 'The user could not be added to the team.'));
    }
  }
}

function problemMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse) || !error.error || typeof error.error !== 'object') {
    return fallback;
  }

  const body = error.error as { title?: string; errors?: Record<string, string[]> };
  const detail = body.errors && Object.values(body.errors).flat().find((item) => item.length > 0);
  return detail || body.title || fallback;
}
