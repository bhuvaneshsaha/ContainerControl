import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AppListResponse, AppResponse, HostListResponse, TeamListResponse } from '../../core/api-models';

@Component({
  selector: 'app-apps',
  imports: [ReactiveFormsModule],
  templateUrl: './apps.html',
})
export class Apps {
  private readonly http = inject(HttpClient);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
  readonly detail = signal('');
  readonly apps = signal<readonly AppResponse[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly hosts = signal<{ id: string; name: string }[]>([]);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    hostId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    environment: new FormControl('dev', { nonNullable: true, validators: [Validators.required] }),
    image: new FormControl('', { nonNullable: true }),
    composeYaml: new FormControl('', { nonNullable: true }),
    internalPort: new FormControl('', { nonNullable: true }),
    hostname: new FormControl('', { nonNullable: true }),
    exposed: new FormControl(false, { nonNullable: true }),
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
    if (this.form.invalid || (!this.form.controls.image.value && !this.form.controls.composeYaml.value)) {
      this.message.set('Enter a name, team, host, and an image or compose file.');
      return;
    }

    const value = this.form.getRawValue();
    try {
      await firstValueFrom(
        this.http.post(`${environment.apiUrl}/apps`, {
          ...value,
          internalPort: value.internalPort ? Number(value.internalPort) : null,
          hostname: value.hostname || null,
          image: value.image || null,
          composeYaml: value.composeYaml || null,
        }),
      );
      await this.load();
    } catch {
      this.message.set('The application could not be saved.');
    }
  }

  async act(app: AppResponse, action: 'deploy' | 'start' | 'stop' | 'restart' | 'rollback'): Promise<void> {
    this.message.set('');
    try {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/apps/${app.id}/${action}`, {}));
      await this.load();
    } catch {
      this.message.set('The application action could not be completed.');
    }
  }

  async logs(app: AppResponse): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<{ text: string }>(`${environment.apiUrl}/apps/${app.id}/logs`),
      );
      this.detail.set(response.text || 'This application has no log output yet.');
    } catch {
      this.detail.set('Logs could not be loaded.');
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
    } catch {
      this.detail.set('Stats could not be loaded.');
    }
  }
}
