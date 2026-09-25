import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
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
import { runLoad } from '../../core/load-state';
import { PermissionService } from '../../core/permissions';
import { problemMessage } from '../../core/problem-message';
import { CheckboxField } from '../../shared/checkbox-field';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { formatTimestamp } from '../../shared/format-time';
import { HasPermission } from '../../shared/has-permission';
import { PageState } from '../../shared/page-state';
import { RecordList, RecordRow } from '../../shared/record-list';
import { SectionBlock } from '../../shared/section-block';
import { SelectOption, SelectField } from '../../shared/select-field';
import { TextField } from '../../shared/text-field';

@Component({
  selector: 'app-access',
  imports: [
    ReactiveFormsModule,
    HasPermission,
    MatButtonModule,
    CheckboxField,
    TextField,
    SelectField,
    PageState,
    RecordList,
    RecordRow,
    SectionBlock,
  ],
  templateUrl: './access.html',
})
export class Access {
  private readonly http = inject(HttpClient);
  private readonly permissions = inject(PermissionService);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
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

  isBusy(key: string): boolean {
    return this.busy().has(key);
  }

  fieldError(control: AbstractControl, messages: Record<string, string>): string {
    return controlError(control, messages);
  }

  canManageUsers(): boolean {
    return this.permissions.hasPermission('access.users.manage');
  }

  formatTime(value: string): string {
    return formatTimestamp(value);
  }

  editingRoleName(): string {
    const id = this.editingRoleId();
    return this.roles().find((role) => role.id === id)?.name ?? '';
  }

  permissionGroups(): { module: string; items: PermissionCatalogItem[] }[] {
    const groups: { module: string; items: PermissionCatalogItem[] }[] = [];
    for (const item of this.catalog()) {
      const existing = groups.find((group) => group.module === item.module);
      if (existing) {
        existing.items.push(item);
      } else {
        groups.push({ module: item.module, items: [item] });
      }
    }
    return groups;
  }

  roleOptions(): SelectOption[] {
    return [{ value: '', label: 'No role yet' }, ...this.roles().map((role) => ({ value: role.id, label: role.name }))];
  }

  teamOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a team' }, ...this.teams().map((team) => ({ value: team.id, label: team.name }))];
  }

  userOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a user' }, ...this.users().map((user) => ({ value: user.id, label: user.displayName }))];
  }

  permissionOptions(): SelectOption[] {
    return [
      { value: '', label: 'Choose a permission' },
      ...this.catalog().map((item) => ({ value: item.code, label: `${item.displayName} (${item.code})` })),
    ];
  }

  grantUserLabel(userId: string): string {
    return this.users().find((user) => user.id === userId)?.displayName ?? userId;
  }

  setCode(code: string, checked: boolean): void {
    const current = this.selectedCodes();
    if (checked) {
      this.selectedCodes.set(current.includes(code) ? current : [...current, code]);
      return;
    }

    this.selectedCodes.set(current.filter((item) => item !== code));
  }

  editRole(role: RoleSummary): void {
    this.editingRoleId.set(role.id);
    this.roleForm.setValue({ name: role.name, description: role.description ?? '' });
    this.selectedCodes.set(role.permissionCodes);
  }

  cancelRole(): void {
    if (this.isBusy('role')) {
      return;
    }

    this.editingRoleId.set(null);
    this.selectedCodes.set([]);
    this.roleForm.reset({ name: '', description: '' });
  }

  async load(): Promise<void> {
    await runLoad(this.status, this.refreshing, this.refreshError, async () => {
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
    });
  }

  async createUser(): Promise<void> {
    this.feedback.clear();
    this.userForm.markAllAsTouched();
    if (this.userForm.invalid) {
      focusFirstInvalid([
        { control: this.userForm.controls.email, id: 'user-email' },
        { control: this.userForm.controls.displayName, id: 'user-name' },
        { control: this.userForm.controls.password, id: 'user-password' },
      ]);
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
    this.teamForm.markAllAsTouched();
    if (this.teamForm.invalid || !this.teamForm.controls.name.value.trim()) {
      if (!this.teamForm.controls.name.value.trim()) {
        this.teamForm.controls.name.setErrors({ required: true });
      }
      focusFirstInvalid([{ control: this.teamForm.controls.name, id: 'team-name' }]);
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
    this.memberForm.markAllAsTouched();
    if (this.memberForm.invalid) {
      focusFirstInvalid([
        { control: this.memberForm.controls.teamId, id: 'member-team' },
        { control: this.memberForm.controls.userId, id: 'member-user' },
      ]);
      return;
    }

    const value = this.memberForm.getRawValue();
    const user = this.users().find((item) => item.id === value.userId);
    const team = this.teams().find((item) => item.id === value.teamId);
    await runBusy(this.busy, 'member', async () => {
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/access/teams/${value.teamId}/members`, { userId: value.userId }));
        this.memberForm.reset({ teamId: '', userId: '' });
        this.feedback.success(`${user?.displayName ?? 'The user'} was added to ${team?.name ?? 'the team'}.`);
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The user could not be added to the team.'));
      }
    });
  }

  async saveRole(): Promise<void> {
    this.feedback.clear();
    this.roleForm.markAllAsTouched();
    if (this.roleForm.invalid || !this.roleForm.controls.name.value.trim()) {
      if (!this.roleForm.controls.name.value.trim()) {
        this.roleForm.controls.name.setErrors({ required: true });
      }
      focusFirstInvalid([{ control: this.roleForm.controls.name, id: 'role-name' }]);
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

  async grant(): Promise<void> {
    this.feedback.clear();
    this.grantForm.markAllAsTouched();
    if (this.grantForm.invalid) {
      focusFirstInvalid([
        { control: this.grantForm.controls.userId, id: 'grant-user' },
        { control: this.grantForm.controls.permissionCode, id: 'grant-permission' },
        { control: this.grantForm.controls.minutes, id: 'grant-minutes' },
      ]);
      return;
    }

    const value = this.grantForm.getRawValue();
    await runBusy(this.busy, 'grant', async () => {
      try {
        await firstValueFrom(
          this.http.post(`${environment.apiUrl}/access/break-glass`, {
            userId: value.userId.trim(),
            permissionCode: value.permissionCode,
            minutes: Number(value.minutes),
          }),
        );
        this.feedback.success('The grant was saved. It is checked by the permission API.');
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The grant could not be saved.'));
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
