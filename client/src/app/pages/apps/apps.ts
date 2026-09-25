import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HubConnection } from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AppListResponse, AppResponse, HostListResponse, SecretListResponse, SecretResponse, TeamListResponse } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { appendLogLine, startLogTail } from '../../core/log-tail';
import { PermissionService } from '../../core/permissions';
import { problemMessage } from '../../core/problem-message';
import { HasPermission } from '../../shared/has-permission';
import { composeServiceNames, secretTargetsForSave } from './compose-services';

type AppAction = 'deploy' | 'approve' | 'start' | 'stop' | 'restart' | 'rollback';

@Component({
  selector: 'app-apps',
  imports: [ReactiveFormsModule, HasPermission],
  templateUrl: './apps.html',
})
export class Apps {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);
  private readonly permissions = inject(PermissionService);
  private logConnection: HubConnection | null = null;

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly busy = signal<string | null>(null);
  readonly detail = signal('');
  readonly apps = signal<readonly AppResponse[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly hosts = signal<{ id: string; name: string }[]>([]);
  readonly selected = signal<AppResponse | null>(null);
  readonly secrets = signal<readonly SecretResponse[]>([]);
  readonly serviceChoices = signal<readonly string[]>([]);
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

  actionKey(app: AppResponse, action: AppAction): string {
    return `${action}:${app.id}`;
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
    this.status.set('loading');
    try {
      const [apps, teams, hosts] = await Promise.all([
        firstValueFrom(this.http.get<AppListResponse>(`${environment.apiUrl}/apps`)),
        firstValueFrom(this.http.get<TeamListResponse>(`${environment.apiUrl}/access/teams`)),
        firstValueFrom(this.http.get<HostListResponse>(`${environment.apiUrl}/platform/hosts/choices`)),
      ]);
      this.apps.set(apps.apps);
      this.teams.set(teams.teams);
      this.hosts.set(hosts.hosts);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  private async refreshApps(): Promise<void> {
    const response = await firstValueFrom(this.http.get<AppListResponse>(`${environment.apiUrl}/apps`));
    this.apps.set(response.apps);
    const selected = this.selected();
    if (selected) {
      const next = response.apps.find((item) => item.id === selected.id) ?? null;
      this.selected.set(next);
      if (!next) {
        this.secrets.set([]);
      }
    }
  }

  async create(): Promise<void> {
    this.feedback.clear();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.feedback.error('Enter a name, team, host, and a compose file.');
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

  teamName(app: AppResponse): string {
    return this.teams().find((team) => team.id === app.teamId)?.name ?? 'Unknown team';
  }

  hostKnown(hostId: string): boolean {
    return this.hosts().some((host) => host.id === hostId);
  }

  openEdit(app: AppResponse): void {
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
    this.editing.set(null);
  }

  async saveEdit(): Promise<void> {
    const app = this.editing();
    this.feedback.clear();
    if (!app || this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      this.feedback.error('Enter a name and a Docker host.');
      return;
    }

    const value = this.editForm.getRawValue();
    if (!value.name.trim() || !value.hostId) {
      this.editForm.markAllAsTouched();
      this.feedback.error('Enter a name and a Docker host.');
      return;
    }

    const image = value.image.trim();
    const composeYaml = value.composeYaml.trim();
    if (!image && !composeYaml) {
      this.feedback.error('Enter an image or a compose file.');
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
        await firstValueFrom(
          this.http.put(`${environment.apiUrl}/apps/${app.id}`, body, { observe: 'response', responseType: 'text' }),
        );
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
        await firstValueFrom(
          this.http.delete(`${environment.apiUrl}/apps/${app.id}`, { observe: 'response', responseType: 'text' }),
        );
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The application could not be removed.'));
        return;
      }

      if (this.editing()?.id === app.id) {
        this.editing.set(null);
      }

      if (this.selected()?.id === app.id) {
        this.selected.set(null);
        this.secrets.set([]);
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
  }

  serviceTargetLabel(secret: SecretResponse): string {
    return secret.serviceNames.length > 0 ? secret.serviceNames.join(', ') : '(none)';
  }

  async openSecrets(app: AppResponse, keepNotice = false): Promise<void> {
    this.selected.set(app);
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
    const app = this.selected();
    this.feedback.clear();
    if (!app || this.secretForm.invalid) {
      this.secretForm.markAllAsTouched();
      this.feedback.error('Enter a secret name and value.');
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
      this.feedback.error('Select at least one service for this secret.');
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
    const app = this.selected();
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

  async live(app: AppResponse): Promise<void> {
    this.detail.set('');
    await runBusy(this.busy, `live:${app.id}`, async () => {
      try {
        await this.logConnection?.stop();
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

  async stats(app: AppResponse): Promise<void> {
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
