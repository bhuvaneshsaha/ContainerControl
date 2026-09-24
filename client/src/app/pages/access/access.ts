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

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
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

  async saveRole(): Promise<void> {
    this.message.set('');
    if (this.roleForm.invalid) {
      this.message.set('Enter a role name.');
      return;
    }

    const value = this.roleForm.getRawValue();
    const body = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      permissionCodes: this.selectedCodes(),
    };
    const roleId = this.editingRoleId();
    try {
      if (roleId) {
        await firstValueFrom(this.http.put(`${environment.apiUrl}/access/roles/${roleId}`, body));
      } else {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/access/roles`, body));
      }
      this.editingRoleId.set(null);
      this.selectedCodes.set([]);
      this.roleForm.reset({ name: '', description: '' });
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The role could not be saved.'));
    }
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
    this.message.set('');
    if (this.grantForm.invalid) {
      this.message.set('Enter a user, a catalog permission, and 5 to 60 minutes.');
      return;
    }

    const value = this.grantForm.getRawValue();
    try {
      await firstValueFrom(
        this.http.post(`${environment.apiUrl}/access/break-glass`, {
          userId: value.userId.trim(),
          permissionCode: value.permissionCode,
          minutes: value.minutes,
        }),
      );
      this.message.set('The grant was saved. It is checked by the permission API.');
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The grant could not be saved.'));
    }
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
