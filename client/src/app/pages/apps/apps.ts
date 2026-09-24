import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HubConnection } from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AppListResponse, AppResponse, HostListResponse, SecretListResponse, SecretResponse, TeamListResponse } from '../../core/api-models';
import { appendLogLine, startLogTail } from '../../core/log-tail';
import { problemMessage } from '../../core/problem-message';
import { HasPermission } from '../../shared/has-permission';

@Component({
  selector: 'app-apps',
  imports: [ReactiveFormsModule, HasPermission],
  templateUrl: './apps.html',
})
export class Apps {
  private readonly http = inject(HttpClient);
  private logConnection: HubConnection | null = null;

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
  readonly detail = signal('');
  readonly apps = signal<readonly AppResponse[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly hosts = signal<{ id: string; name: string }[]>([]);
  readonly selected = signal<AppResponse | null>(null);
  readonly secrets = signal<readonly SecretResponse[]>([]);
  readonly secretForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    injectionMode: new FormControl('env', { nonNullable: true, validators: [Validators.required] }),
    value: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
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
  });

  constructor() {
    void this.load();
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

  async create(): Promise<void> {
    this.message.set('');
    if (this.form.invalid) {
      this.message.set('Enter a name, team, host, and a compose file.');
      return;
    }

    const value = this.form.getRawValue();
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
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The application could not be saved.'));
    }
  }

  async openSecrets(app: AppResponse): Promise<void> {
    this.selected.set(app);
    this.message.set('');
    try {
      const response = await firstValueFrom(
        this.http.get<SecretListResponse>(
          `${environment.apiUrl}/secrets?teamId=${app.teamId}&environment=${app.environment}`,
        ),
      );
      this.secrets.set(response.secrets);
    } catch (error) {
      this.secrets.set([]);
      this.message.set(problemMessage(error, 'Secrets could not be loaded.'));
    }
  }

  async saveSecret(): Promise<void> {
    const app = this.selected();
    this.message.set('');
    if (!app || this.secretForm.invalid) {
      this.message.set('Enter a secret name and value.');
      return;
    }

    const value = this.secretForm.getRawValue();
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
          },
          { observe: 'response', responseType: 'text' },
        ),
      );
      this.secretForm.controls.value.setValue('');
      await this.openSecrets(app);
    } catch (error) {
      this.message.set(problemMessage(error, 'The secret could not be saved.'));
    }
  }

  async deleteSecret(secret: SecretResponse): Promise<void> {
    const app = this.selected();
    if (!app) {
      return;
    }

    this.message.set('');
    try {
      await firstValueFrom(this.http.delete(`${environment.apiUrl}/secrets/${secret.id}`));
      await this.openSecrets(app);
    } catch (error) {
      this.message.set(problemMessage(error, 'The secret could not be deleted.'));
    }
  }

  async act(app: AppResponse, action: 'deploy' | 'approve' | 'start' | 'stop' | 'restart' | 'rollback'): Promise<void> {
    this.message.set('');
    try {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/apps/${app.id}/${action}`, {}));
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The application action could not be completed.'));
    }
  }

  async live(app: AppResponse): Promise<void> {
    this.detail.set('');
    try {
      await this.logConnection?.stop();
      const csrf = await firstValueFrom(
        this.http.get<{ token?: string }>(`${environment.apiUrl}/auth/csrf`),
      );
      if (!csrf.token) {
        this.detail.set('The log stream could not be started.');
        return;
      }

      this.logConnection = await startLogTail(app.id, csrf.token, (line) => {
        this.detail.set(appendLogLine(this.detail(), line));
      });
    } catch (error) {
      this.detail.set(problemMessage(error, 'The log stream could not be started.'));
    }
  }

  async logs(app: AppResponse): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<{ text: string }>(`${environment.apiUrl}/apps/${app.id}/logs`),
      );
      this.detail.set(response.text || 'This application has no log output yet.');
    } catch (error) {
      this.detail.set(problemMessage(error, 'Logs could not be loaded.'));
    }
  }

  async stats(app: AppResponse): Promise<void> {
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
      this.detail.set(problemMessage(error, 'Stats could not be loaded.'));
    }
  }
}
