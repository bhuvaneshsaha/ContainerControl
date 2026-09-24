import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AuditEntry,
  AuditListResponse,
  PermissionCatalogItem,
  PermissionCatalogResponse,
  RoleListResponse,
  RoleSummary,
  TeamListResponse,
  UserListResponse,
  UserSummary,
} from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { PermissionService } from '../../core/permissions';
import { problemMessage } from '../../core/problem-message';
import { HasPermission } from '../../shared/has-permission';

@Component({
  selector: 'app-access',
  imports: [ReactiveFormsModule, HasPermission],
  templateUrl: './access.html',
})
export class Access {
  private readonly http = inject(HttpClient);
  private readonly permissions = inject(PermissionService);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly busy = signal<string | null>(null);
  readonly users = signal<readonly UserSummary[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly roles = signal<readonly RoleSummary[]>([]);
  readonly catalog = signal<readonly PermissionCatalogItem[]>([]);
  readonly audit = signal<readonly AuditEntry[]>([]);
  readonly grants = signal<readonly { id: string; userId: string; permissionCode: string; expiresAtUtc: string }[]>([]);
  readonly selectedCodes = signal<readonly string[]>([]);
  readonly editingRoleId = signal<string | null>(null);
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
  readonly roleForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true }),
  });
  readonly grantForm = new FormGroup({
    userId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    permissionCode: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    minutes: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(5), Validators.max(60)] }),
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const tasks: Promise<void>[] = [];
      if (this.permissions.hasPermission('access.users.manage')) {
        tasks.push(this.loadUsers());
      }

      if (this.permissions.hasPermission('access.users.manage') || this.permissions.hasPermission('access.teams.manage')) {
        tasks.push(this.loadTeams());
      }

      if (this.permissions.hasPermission('access.roles.manage') || this.permissions.hasPermission('access.users.manage')) {
        tasks.push(this.loadRoles());
      }

      if (this.permissions.hasPermission('access.roles.manage')) {
        tasks.push(this.loadCatalog());
      }

      if (this.permissions.hasPermission('access.audit.read')) {
        tasks.push(this.loadAudit());
      }

      if (this.permissions.hasPermission('access.breakglass.grant')) {
        tasks.push(this.loadGrants());
      }

      await Promise.all(tasks);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  toggleCode(code: string): void {
    const current = this.selectedCodes();
    this.selectedCodes.set(current.includes(code) ? current.filter((item) => item !== code) : [...current, code]);
  }

  editRole(role: RoleSummary): void {
    this.editingRoleId.set(role.id);
    this.roleForm.setValue({ name: role.name, description: role.description ?? '' });
    this.selectedCodes.set(role.permissionCodes);
  }

  async createUser(): Promise<void> {
    this.feedback.clear();
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      this.feedback.error('Enter an email, a display name, and a password.');
      return;
    }

    const value = this.userForm.getRawValue();
    await runBusy(this.busy, 'user', async () => {
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
        this.feedback.success(`${value.displayName.trim()} was created.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The user could not be created.'));
      }
    });
  }

  async disable(user: UserSummary): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: `Disable ${user.displayName}?`,
      body: `${user.displayName} (${user.email}) will not be able to sign in. This page cannot enable the account again.`,
      confirmLabel: 'Disable user',
      irreversible: true,
    });
    if (!confirmed) {
      return;
    }

    await runBusy(this.busy, `disable:${user.id}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/access/users/${user.id}/disable`, {}));
        this.feedback.success(`${user.displayName} was disabled.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The user could not be disabled.'));
      }
    });
  }

  async createTeam(): Promise<void> {
    this.feedback.clear();
    if (this.teamForm.invalid) {
      this.teamForm.markAllAsTouched();
      this.feedback.error('Enter a team name.');
      return;
    }

    const name = this.teamForm.controls.name.value.trim();
    await runBusy(this.busy, 'team', async () => {
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/access/teams`, { name }));
        this.teamForm.reset({ name: '' });
        this.feedback.success(`Team ${name} was created.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The team could not be created.'));
      }
    });
  }

  async addMember(): Promise<void> {
    this.feedback.clear();
    if (this.memberForm.invalid) {
      this.memberForm.markAllAsTouched();
      this.feedback.error('Select a team and a user.');
      return;
    }

    const value = this.memberForm.getRawValue();
    await runBusy(this.busy, 'member', async () => {
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/access/teams/${value.teamId}/members`, { userId: value.userId }));
        this.feedback.success('The user was added to the team.');
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The user could not be added to the team.'));
      }
    });
  }

  async saveRole(): Promise<void> {
    this.feedback.clear();
    if (this.roleForm.invalid) {
      this.roleForm.markAllAsTouched();
      this.feedback.error('Enter a role name.');
      return;
    }

    const value = this.roleForm.getRawValue();
    const body = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      permissionCodes: this.selectedCodes(),
    };
    const roleId = this.editingRoleId();
    await runBusy(this.busy, 'role', async () => {
      try {
        if (roleId) {
          await firstValueFrom(this.http.put(`${environment.apiUrl}/access/roles/${roleId}`, body));
        } else {
          await firstValueFrom(this.http.post(`${environment.apiUrl}/access/roles`, body));
        }
        this.editingRoleId.set(null);
        this.selectedCodes.set([]);
        this.roleForm.reset({ name: '', description: '' });
        this.feedback.success(`Role ${body.name} was saved.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The role could not be saved.'));
      }
    });
  }

  private async loadUsers(): Promise<void> {
    const users = await firstValueFrom(this.http.get<UserListResponse>(`${environment.apiUrl}/access/users`));
    this.users.set(users.users);
  }

  private async loadTeams(): Promise<void> {
    const teams = await firstValueFrom(this.http.get<TeamListResponse>(`${environment.apiUrl}/access/teams`));
    this.teams.set(teams.teams);
  }

  private async loadRoles(): Promise<void> {
    const roles = await firstValueFrom(this.http.get<RoleListResponse>(`${environment.apiUrl}/access/roles`));
    this.roles.set(roles.roles);
  }

  private async loadCatalog(): Promise<void> {
    const catalog = await firstValueFrom(this.http.get<PermissionCatalogResponse>(`${environment.apiUrl}/permissions`));
    this.catalog.set(catalog.permissions);
  }

  async grant(): Promise<void> {
    this.feedback.clear();
    if (this.grantForm.invalid) {
      this.grantForm.markAllAsTouched();
      this.feedback.error('Enter a user, a catalog permission, and 5 to 60 minutes.');
      return;
    }

    const value = this.grantForm.getRawValue();
    await runBusy(this.busy, 'grant', async () => {
      try {
        await firstValueFrom(
          this.http.post(`${environment.apiUrl}/access/break-glass`, {
            userId: value.userId.trim(),
            permissionCode: value.permissionCode,
            minutes: value.minutes,
          }),
        );
        this.feedback.success('The grant was saved. It is checked by the permission API.');
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The grant could not be saved.'));
      }
    });
  }

  private async loadGrants(): Promise<void> {
    const response = await firstValueFrom(
      this.http.get<{ grants: { id: string; userId: string; permissionCode: string; expiresAtUtc: string }[]; permissions: PermissionCatalogItem[] }>(
        `${environment.apiUrl}/access/break-glass`,
      ),
    );
    this.grants.set(response.grants);
    this.catalog.set(response.permissions);
  }

  private async loadAudit(): Promise<void> {
    const audit = await firstValueFrom(this.http.get<AuditListResponse>(`${environment.apiUrl}/access/audit`));
    this.audit.set(audit.entries);
  }
}
