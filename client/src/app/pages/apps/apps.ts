import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { HubConnection } from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AppListResponse, AppResponse, HostListResponse, SecretListResponse, SecretResponse, TeamListResponse } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { runLoad } from '../../core/load-state';
import { appendLogLine, startLogTail } from '../../core/log-tail';
import { PermissionService } from '../../core/permissions';
import { problemMessage } from '../../core/problem-message';
import { ActionCluster } from '../../shared/action-cluster';
import { CheckboxField } from '../../shared/checkbox-field';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { HasPermission } from '../../shared/has-permission';
import { PageState } from '../../shared/page-state';
import { RecordList, RecordRow } from '../../shared/record-list';
import { SelectOption, SelectField } from '../../shared/select-field';
import { StatusBadge } from '../../shared/status-badge';
import { TextField } from '../../shared/text-field';
import { composeServiceNames, secretTargetsForSave } from './compose-services';

type AppAction = 'deploy' | 'approve' | 'start' | 'stop' | 'restart' | 'rollback';
type InspectKind = 'secrets' | 'logs' | 'stored' | 'live' | 'stats';

@Component({
  selector: 'app-apps',
  imports: [
    ReactiveFormsModule,
    HasPermission,
    MatButtonModule,
    TextField,
    SelectField,
    CheckboxField,
    PageState,
    RecordList,
    RecordRow,
    StatusBadge,
    ActionCluster,
  ],
  templateUrl: './apps.html',
})
export class Apps {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);
  private readonly permissions = inject(PermissionService);
  private readonly createDetails = viewChild<ElementRef<HTMLDetailsElement>>('createDetails');
  private logConnection: HubConnection | null = null;

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly canaryPercent = signal(10);
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly detail = signal('');
  readonly apps = signal<readonly AppResponse[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly hosts = signal<{ id: string; name: string }[]>([]);
  readonly inspect = signal<{ appId: string; kind: InspectKind } | null>(null);
  readonly secrets = signal<readonly SecretResponse[]>([]);
  readonly serviceChoices = signal<readonly string[]>([]);
  readonly secretServiceError = signal('');
  readonly secretForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    injectionMode: new FormControl('env', { nonNullable: true, validators: [Validators.required] }),
    value: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    serviceNames: new FormControl<string[]>([], { nonNullable: true }),
  });
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    hostId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    environment: new FormControl('dev', { nonNullable: true, validators: [Validators.required] }),
    composeYaml: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    internalPort: new FormControl('', { nonNullable: true }),
    hostname: new FormControl('', { nonNullable: true }),
    exposed: new FormControl(false, { nonNullable: true }),
    requireApproval: new FormControl(false, { nonNullable: true }),
    allowDatabaseImages: new FormControl(false, { nonNullable: true }),
  });
  readonly editing = signal<AppResponse | null>(null);
  readonly editForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    hostId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    image: new FormControl('', { nonNullable: true }),
    composeYaml: new FormControl('', { nonNullable: true }),
    command: new FormControl('', { nonNullable: true }),
    internalPort: new FormControl('', { nonNullable: true }),
    hostname: new FormControl('', { nonNullable: true }),
    exposed: new FormControl(false, { nonNullable: true }),
    requireApproval: new FormControl(false, { nonNullable: true }),
    allowDatabaseImages: new FormControl(false, { nonNullable: true }),
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

  can(code: string): boolean {
    return this.permissions.hasPermission(code);
  }

  actionKey(app: AppResponse, action: AppAction): string {
    return `${action}:${app.id}`;
  }

  inspectedApp(): AppResponse | null {
    const current = this.inspect();
    if (!current) {
      return null;
    }

    return this.apps().find((item) => item.id === current.appId) ?? null;
  }

  inspectTitle(app: AppResponse): string {
    switch (this.inspect()?.kind) {
      case 'secrets':
        return `Secrets for ${app.name}`;
      case 'logs':
        return `Logs for ${app.name}`;
      case 'stored':
        return `Stored logs for ${app.name}`;
      case 'live':
        return `Live logs for ${app.name}`;
      case 'stats':
        return `Stats for ${app.name}`;
      default:
        return app.name;
    }
  }

  secretWriteState(app: AppResponse): 'form' | 'prod' | 'hidden' {
    if (!this.permissions.hasPermission('secrets.manage')) {
      return 'hidden';
    }

    if (app.environment === 'prod' && !this.permissions.hasPermission('secrets.manage.prod')) {
      return 'prod';
    }

    return 'form';
  }

  async load(): Promise<void> {
    await runLoad(this.status, this.refreshing, this.refreshError, async () => {
      const [apps, teams, hosts] = await Promise.all([
        firstValueFrom(this.http.get<AppListResponse>(`${environment.apiUrl}/apps`)),
        firstValueFrom(this.http.get<TeamListResponse>(`${environment.apiUrl}/access/teams`)),
        firstValueFrom(this.http.get<HostListResponse>(`${environment.apiUrl}/platform/hosts/choices`)),
      ]);
      this.apps.set(apps.apps);
      this.teams.set(teams.teams);
      this.hosts.set(hosts.hosts);
      const current = this.inspect();
      if (current && !apps.apps.some((item) => item.id === current.appId)) {
        this.inspect.set(null);
        this.secrets.set([]);
        await this.stopHub();
      }
    });
  }

  private async refreshApps(): Promise<void> {
    const response = await firstValueFrom(this.http.get<AppListResponse>(`${environment.apiUrl}/apps`));
    this.apps.set(response.apps);
    const current = this.inspect();
    if (current && !response.apps.some((item) => item.id === current.appId)) {
      this.inspect.set(null);
      this.secrets.set([]);
      await this.stopHub();
    }
  }

  async create(): Promise<void> {
    this.feedback.clear();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid([
        { control: this.form.controls.name, id: 'app-name' },
        { control: this.form.controls.teamId, id: 'app-team' },
        { control: this.form.controls.hostId, id: 'app-host' },
        { control: this.form.controls.composeYaml, id: 'app-compose' },
      ]);
      return;
    }

    const value = this.form.getRawValue();
    await runBusy(this.busy, 'create', async () => {
      try {
        await firstValueFrom(
          this.http.post(`${environment.apiUrl}/apps`, {
            ...value,
            internalPort: value.internalPort ? Number(value.internalPort) : null,
            hostname: value.hostname || null,
            image: null,
            composeYaml: value.composeYaml,
            requiresApproval: value.environment === 'prod' || value.requireApproval,
          }),
        );
        this.feedback.success(`${value.name.trim()} was saved.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application could not be saved.'));
      }
    });
  }

  readonly environmentOptions: readonly SelectOption[] = [
    { value: 'dev', label: 'dev' },
    { value: 'staging', label: 'staging' },
    { value: 'prod', label: 'prod' },
  ];
  readonly injectionOptions: readonly SelectOption[] = [
    { value: 'env', label: 'Environment variable' },
    { value: 'file', label: 'File' },
  ];

  teamName(app: AppResponse): string {
    return this.teams().find((team) => team.id === app.teamId)?.name ?? 'Unknown team';
  }

  teamOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a team' }, ...this.teams().map((team) => ({ value: team.id, label: team.name }))];
  }

  hostOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a host' }, ...this.hosts().map((host) => ({ value: host.id, label: host.name }))];
  }

  editHostOptions(app: AppResponse): SelectOption[] {
    const options = this.hostOptions();
    if (!this.hostKnown(app.hostId)) {
      return [options[0], { value: app.hostId, label: 'Current host' }, ...options.slice(1)];
    }

    return options;
  }

  hostKnown(hostId: string): boolean {
    return this.hosts().some((host) => host.id === hostId);
  }

  async openEdit(app: AppResponse): Promise<void> {
    await this.stopHub();
    const details = this.createDetails()?.nativeElement;
    if (details) {
      details.open = false;
    }

    this.editing.set(app);
    this.editForm.controls.requireApproval.enable();
    this.editForm.setValue({
      name: app.name,
      hostId: app.hostId,
      image: app.image ?? '',
      composeYaml: app.composeYaml ?? '',
      command: (app.command ?? []).join('\n'),
      internalPort: app.internalPort == null ? '' : String(app.internalPort),
      hostname: app.hostname ?? '',
      exposed: app.exposed,
      requireApproval: app.environment === 'prod' || app.requiresApproval,
      allowDatabaseImages: app.allowDatabaseImages,
    });
    if (app.environment === 'prod') {
      this.editForm.controls.requireApproval.disable();
    }

    this.feedback.clear();
  }

  cancelEdit(): void {
    if (this.isBusy('edit')) {
      return;
    }

    this.editing.set(null);
  }

  async saveEdit(): Promise<void> {
    const app = this.editing();
    this.feedback.clear();
    this.clearImageOrComposeError();
    if (!this.editForm.controls.name.value.trim()) {
      this.editForm.controls.name.setErrors({ required: true });
    }

    this.editForm.markAllAsTouched();
    if (!app || this.editForm.invalid) {
      focusFirstInvalid([
        { control: this.editForm.controls.name, id: 'edit-app-name' },
        { control: this.editForm.controls.hostId, id: 'edit-app-host' },
      ]);
      return;
    }

    const value = this.editForm.getRawValue();
    const image = value.image.trim();
    const composeYaml = value.composeYaml.trim();
    if (!image && !composeYaml) {
      this.editForm.controls.image.setErrors({ imageOrCompose: true });
      this.editForm.controls.composeYaml.setErrors({ imageOrCompose: true });
      this.editForm.controls.image.markAsTouched();
      this.editForm.controls.composeYaml.markAsTouched();
      focusFirstInvalid([
        { control: this.editForm.controls.image, id: 'edit-app-image' },
        { control: this.editForm.controls.composeYaml, id: 'edit-app-compose' },
      ]);
      return;
    }

    const body: {
      name: string;
      hostId: string;
      image: string | null;
      composeYaml: string | null;
      command: string[] | null;
      internalPort: number | null;
      hostname: string | null;
      exposed: boolean;
      requiresApproval: boolean;
      allowDatabaseImages?: boolean;
    } = {
      name: value.name.trim(),
      hostId: value.hostId,
      image: image || null,
      composeYaml: composeYaml || null,
      command: commandLines(value.command),
      internalPort: value.internalPort ? Number(value.internalPort) : null,
      hostname: value.hostname.trim() || null,
      exposed: value.exposed,
      requiresApproval: app.environment === 'prod' || value.requireApproval,
    };
    if (this.permissions.hasPermission('platform.settings.manage')) {
      body.allowDatabaseImages = value.allowDatabaseImages;
    }

    await runBusy(this.busy, 'edit', async () => {
      try {
        await firstValueFrom(this.http.put(`${environment.apiUrl}/apps/${app.id}`, body));
        this.editing.set(null);
        this.feedback.success(`${body.name} was saved.`);
        try {
          await this.refreshApps();
        } catch (error) {
          this.feedback.error(problemMessage(error, 'The application was saved, but the list could not be refreshed.'));
        }
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application could not be saved.'));
      }
    });
  }

  async remove(app: AppResponse): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: `Remove ${app.name}?`,
      body: `This deletes ${app.name} and removes its containers from the Docker host. Deploy history for this application is removed.`,
      confirmLabel: 'Remove application',
      irreversible: true,
    });
    if (!confirmed) {
      return;
    }

    await runBusy(this.busy, `remove:${app.id}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.delete(`${environment.apiUrl}/apps/${app.id}`));
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application could not be removed.'));
        return;
      }

      if (this.editing()?.id === app.id) {
        this.editing.set(null);
      }

      if (this.inspect()?.appId === app.id) {
        this.inspect.set(null);
        this.secrets.set([]);
        await this.stopHub();
      }

      this.apps.update((items) => items.filter((item) => item.id !== app.id));
      this.feedback.success(`${app.name} was removed.`);
      try {
        await this.refreshApps();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application was removed, but the list could not be refreshed.'));
      }
    });
  }

  serviceSelected(name: string): boolean {
    return this.secretForm.controls.serviceNames.value.includes(name);
  }

  toggleService(name: string, checked: boolean): void {
    const next = new Set(this.secretForm.controls.serviceNames.value);
    if (checked) {
      next.add(name);
    } else {
      next.delete(name);
    }

    this.secretForm.controls.serviceNames.setValue([...next]);
    if (next.size > 0) {
      this.secretServiceError.set('');
    }
  }

  serviceTargetLabel(secret: SecretResponse): string {
    return secret.serviceNames.length > 0 ? secret.serviceNames.join(', ') : '(none)';
  }

  async openSecrets(app: AppResponse, keepNotice = false): Promise<void> {
    await this.stopHub();
    this.inspect.set({ appId: app.id, kind: 'secrets' });
    this.detail.set('');
    this.secretServiceError.set('');
    const choices = composeServiceNames(app.composeYaml, app.image);
    this.serviceChoices.set(choices);
    this.secretForm.controls.serviceNames.setValue(choices.length === 1 ? [...choices] : []);
    if (!keepNotice) {
      this.feedback.clear();
    }
    try {
      const response = await firstValueFrom(
        this.http.get<SecretListResponse>(
          `${environment.apiUrl}/secrets?teamId=${app.teamId}&environment=${app.environment}`,
        ),
      );
      this.secrets.set(response.secrets);
    } catch (error) {
      this.secrets.set([]);
      this.feedback.error(problemMessage(error, 'Secrets could not be loaded.'));
    }
  }

  async saveSecret(): Promise<void> {
    const app = this.inspectedApp();
    this.feedback.clear();
    this.secretServiceError.set('');
    this.secretForm.markAllAsTouched();
    if (!app || this.secretForm.invalid) {
      focusFirstInvalid([
        { control: this.secretForm.controls.name, id: 'secret-name' },
        { control: this.secretForm.controls.value, id: 'secret-value' },
      ]);
      return;
    }

    if (this.secretWriteState(app) !== 'form') {
      this.feedback.error('Production secrets need Manage production secrets.');
      return;
    }

    const value = this.secretForm.getRawValue();
    const existing = this.secrets().find((secret) => secret.name === value.name.trim());
    const serviceNames = secretTargetsForSave(value.serviceNames, this.serviceChoices(), existing?.serviceNames);
    if (serviceNames.length === 0) {
      this.secretServiceError.set('Select at least one service for this secret.');
      document.getElementById('secret-services')?.focus();
      return;
    }

    await runBusy(this.busy, 'secret', async () => {
      try {
        await firstValueFrom(
          this.http.post(
            `${environment.apiUrl}/secrets`,
            {
              teamId: app.teamId,
              environment: app.environment,
              name: value.name.trim(),
              injectionMode: value.injectionMode,
              value: value.value,
              serviceNames,
            },
            { observe: 'response', responseType: 'text' },
          ),
        );
        this.secretForm.controls.value.setValue('');
        this.feedback.success(`Secret ${value.name.trim()} was saved.`);
        await this.openSecrets(app, true);
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The secret could not be saved.'));
      }
    });
  }

  async deleteSecret(secret: SecretResponse): Promise<void> {
    const app = this.inspectedApp();
    if (!app) {
      return;
    }

    const confirmed = await this.confirm.ask({
      title: `Delete secret ${secret.name}?`,
      body: `The value for ${secret.name} is removed from ${app.name} and is not shown again.`,
      confirmLabel: 'Delete secret',
      irreversible: true,
    });
    if (!confirmed) {
      return;
    }

    await runBusy(this.busy, `delete:${secret.id}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.delete(`${environment.apiUrl}/secrets/${secret.id}`));
        this.feedback.success(`Secret ${secret.name} was deleted.`);
        await this.openSecrets(app, true);
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The secret could not be deleted.'));
      }
    });
  }

  async act(app: AppResponse, action: AppAction): Promise<void> {
    if (action === 'stop' || action === 'rollback') {
      const confirmed = await this.confirm.ask(
        action === 'stop'
          ? {
              title: `Stop ${app.name}?`,
              body: `Running services for ${app.name} will stop. You can start them again.`,
              confirmLabel: 'Stop application',
            }
          : {
              title: `Roll back ${app.name}?`,
              body: `This replaces the current release of ${app.name} with the last successful desired state.`,
              confirmLabel: 'Roll back',
              irreversible: true,
            },
      );
      if (!confirmed) {
        return;
      }
    }

    await runBusy(this.busy, this.actionKey(app, action), async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/apps/${app.id}/${action}`, {}));
        this.feedback.success(actionResult(app.name, action));
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application action could not be completed.'));
      }
    });
  }

  setCanary(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    if (Number.isInteger(value) && value >= 1 && value <= 99) {
      this.canaryPercent.set(value);
    }
  }

  async slotDeploy(app: AppResponse): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: `Deploy beside ${app.name}?`,
      body: `This starts a second copy of ${app.name} next to the live release. Public traffic stays on the live release until you swap or set a canary.`,
      confirmLabel: 'Deploy beside',
    });
    if (!confirmed) {
      return;
    }

    await this.postSlot(app, 'slot', 'slots/deploy', {}, `${app.name} is running beside the live release.`);
  }

  async swapTraffic(app: AppResponse): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: `Swap traffic for ${app.name}?`,
      body: `Public traffic moves to the release waiting beside ${app.name}. The previous release stays running so you can revert.`,
      confirmLabel: 'Swap traffic',
    });
    if (!confirmed) {
      return;
    }

    await this.postSlot(app, 'swap', 'slots/swap', {}, `Public traffic for ${app.name} moved to the new release.`);
  }

  async canary(app: AppResponse): Promise<void> {
    const percent = this.canaryPercent();
    if (!Number.isInteger(percent) || percent < 1 || percent > 99) {
      this.feedback.error('Enter a canary percent from 1 to 99.');
      return;
    }

    await this.postSlot(app, 'canary', 'slots/canary', { percent }, `${percent}% of public traffic for ${app.name} goes to the new release.`);
  }

  async revertTraffic(app: AppResponse): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: `Revert traffic for ${app.name}?`,
      body: `Public traffic goes back to the previous release of ${app.name}. The other release stays running.`,
      confirmLabel: 'Revert traffic',
    });
    if (!confirmed) {
      return;
    }

    await this.postSlot(app, 'revert', 'slots/revert', {}, `Public traffic for ${app.name} is back on the previous release.`);
  }

  async stopLive(): Promise<void> {
    await this.stopHub();
  }

  async live(app: AppResponse): Promise<void> {
    await this.stopHub();
    this.inspect.set({ appId: app.id, kind: 'live' });
    this.detail.set('');
    await runBusy(this.busy, `live:${app.id}`, async () => {
      try {
        const csrf = await firstValueFrom(
          this.http.get<{ token?: string }>(`${environment.apiUrl}/auth/csrf`),
        );
        if (!csrf.token) {
          this.feedback.error('The log stream could not be started.');
          return;
        }

        this.logConnection = await startLogTail(app.id, csrf.token, (line) => {
          this.detail.set(appendLogLine(this.detail(), line));
        });
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The log stream could not be started.'));
      }
    });
  }

  async logs(app: AppResponse): Promise<void> {
    await this.stopHub();
    this.inspect.set({ appId: app.id, kind: 'logs' });
    this.detail.set('');
    await runBusy(this.busy, `logs:${app.id}`, async () => {
      try {
        const response = await firstValueFrom(
          this.http.get<{ text: string }>(`${environment.apiUrl}/apps/${app.id}/logs`),
        );
        this.detail.set(response.text || 'This application has no log output yet.');
      } catch (error) {
        this.feedback.error(problemMessage(error, 'Logs could not be loaded.'));
      }
    });
  }

  async storedLogs(app: AppResponse): Promise<void> {
    await this.stopHub();
    this.inspect.set({ appId: app.id, kind: 'stored' });
    this.detail.set('');
    await runBusy(this.busy, `stored:${app.id}`, async () => {
      try {
        const response = await firstValueFrom(
          this.http.get<{ lines: { service: string; at: string; text: string }[] }>(
            `${environment.apiUrl}/apps/${app.id}/logs/stored`,
          ),
        );
        this.detail.set(
          response.lines.map((line) => `${line.at} ${line.service} ${line.text}`).join('\n') ||
            'No stored logs yet.',
        );
      } catch (error) {
        this.feedback.error(problemMessage(error, 'Stored logs could not be loaded.'));
      }
    });
  }

  async stats(app: AppResponse): Promise<void> {
    await this.stopHub();
    this.inspect.set({ appId: app.id, kind: 'stats' });
    this.detail.set('');
    await runBusy(this.busy, `stats:${app.id}`, async () => {
      try {
        const response = await firstValueFrom(
          this.http.get<{ services: { service: string; cpuPercent: number; memoryBytes: number }[] }>(
            `${environment.apiUrl}/apps/${app.id}/stats`,
          ),
        );
        this.detail.set(
          response.services.map((item) => `${item.service}: CPU ${item.cpuPercent}%, memory ${item.memoryBytes} bytes`).join('\n') ||
            'No running services returned stats.',
        );
      } catch (error) {
        this.feedback.error(problemMessage(error, 'Stats could not be loaded.'));
      }
    });
  }

  private clearImageOrComposeError(): void {
    for (const control of [this.editForm.controls.image, this.editForm.controls.composeYaml]) {
      if (control.hasError('imageOrCompose')) {
        control.updateValueAndValidity();
      }
    }
  }

  private async postSlot(app: AppResponse, key: string, path: string, body: object, success: string): Promise<void> {
    await runBusy(this.busy, `${key}:${app.id}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/apps/${app.id}/${path}`, body));
        this.feedback.success(success);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application action could not be completed.'));
      }
    });
  }

  private async stopHub(): Promise<void> {
    const connection = this.logConnection;
    this.logConnection = null;
    if (!connection) {
      return;
    }

    try {
      await connection.stop();
    } catch {
      // The tail is already closed.
    }
  }
}

function commandLines(value: string): string[] | null {
  const lines = value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
  return lines.length === 0 ? null : lines;
}

function actionResult(name: string, action: AppAction): string {
  switch (action) {
    case 'deploy':
      return `${name} deploy finished.`;
    case 'approve':
      return `${name} was approved.`;
    case 'start':
      return `${name} is starting.`;
    case 'stop':
      return `${name} was stopped.`;
    case 'restart':
      return `${name} is restarting.`;
    case 'rollback':
      return `${name} was rolled back.`;
  }
}
