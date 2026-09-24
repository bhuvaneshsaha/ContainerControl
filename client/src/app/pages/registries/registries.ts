import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { problemMessage } from '../../core/problem-message';
import { HasPermission } from '../../shared/has-permission';

interface RegistryResponse {
  id: string;
  name: string;
  kind: string;
  server: string;
  environment: string;
}

interface RegistryListResponse {
  registries: RegistryResponse[];
}

@Component({
  selector: 'app-registries',
  imports: [ReactiveFormsModule, HasPermission],
  templateUrl: './registries.html',
})
export class Registries {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);

  readonly kinds = ['Acr', 'Ecr', 'DockerHub', 'Harbor'] as const;
  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly busy = signal<string | null>(null);
  readonly registries = signal<readonly RegistryResponse[]>([]);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    kind: new FormControl('Harbor', { nonNullable: true, validators: [Validators.required] }),
    server: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    environment: new FormControl('dev', { nonNullable: true, validators: [Validators.required] }),
    username: new FormControl('', { nonNullable: true }),
    password: new FormControl('', { nonNullable: true }),
    accessKeyId: new FormControl('', { nonNullable: true }),
    secretAccessKey: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const response = await firstValueFrom(this.http.get<RegistryListResponse>(`${environment.apiUrl}/registries`));
      this.registries.set(response.registries);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  async save(): Promise<void> {
    this.feedback.clear();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.feedback.error('Enter a name, type, and host.');
      return;
    }

    const value = this.form.getRawValue();
    await runBusy(this.busy, 'save', async () => {
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/registries`, value));
        this.form.controls.password.setValue('');
        this.form.controls.secretAccessKey.setValue('');
        this.feedback.success('The registry connection was saved. The password is not shown again.');
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The registry could not be saved.'));
      }
    });
  }
}
