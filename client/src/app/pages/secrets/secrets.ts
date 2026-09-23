import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { SecretListResponse, SecretResponse, TeamListResponse } from '../../core/api-models';

@Component({
  selector: 'app-secrets',
  imports: [ReactiveFormsModule],
  templateUrl: './secrets.html',
})
export class Secrets {
  private readonly http = inject(HttpClient);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
  readonly secrets = signal<readonly SecretResponse[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly form = new FormGroup({
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    environment: new FormControl('dev', { nonNullable: true, validators: [Validators.required] }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    injectionMode: new FormControl('env', { nonNullable: true, validators: [Validators.required] }),
    value: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    void this.loadTeams();
  }

  async loadTeams(): Promise<void> {
    try {
      const teams = await firstValueFrom(this.http.get<TeamListResponse>(`${environment.apiUrl}/access/teams`));
      this.teams.set(teams.teams);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  async load(): Promise<void> {
    const teamId = this.form.controls.teamId.value;
    const environmentName = this.form.controls.environment.value;
    if (!teamId) {
      this.secrets.set([]);
      return;
    }

    this.status.set('loading');
    try {
      const response = await firstValueFrom(
        this.http.get<SecretListResponse>(
          `${environment.apiUrl}/secrets?teamId=${teamId}&environment=${environmentName}`,
        ),
      );
      this.secrets.set(response.secrets);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  async save(): Promise<void> {
    this.message.set('');
    if (this.form.invalid) {
      this.message.set('Enter a team, name, injection mode, and value.');
      return;
    }

    try {
      await firstValueFrom(
        this.http.post(`${environment.apiUrl}/secrets`, this.form.getRawValue(), { responseType: 'text' }),
      );
      this.form.controls.value.setValue('');
      await this.load();
    } catch {
      this.message.set('The secret could not be saved.');
    }
  }
}
