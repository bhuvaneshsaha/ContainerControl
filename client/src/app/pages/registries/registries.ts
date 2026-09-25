import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { runLoad } from '../../core/load-state';
import { problemMessage } from '../../core/problem-message';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { HasPermission } from '../../shared/has-permission';
import { PageState } from '../../shared/page-state';
import { RecordList, RecordRow } from '../../shared/record-list';
import { SelectOption, SelectField } from '../../shared/select-field';
import { TextField } from '../../shared/text-field';

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
  imports: [ReactiveFormsModule, HasPermission, MatButtonModule, TextField, SelectField, PageState, RecordList, RecordRow],
  templateUrl: './registries.html',
})
export class Registries {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);

  readonly kinds = ['Acr', 'Ecr', 'DockerHub', 'Harbor'] as const;
  readonly kindOptions: readonly SelectOption[] = this.kinds.map((kind) => ({ value: kind, label: kind }));
  readonly environmentOptions: readonly SelectOption[] = [
    { value: 'dev', label: 'dev' },
    { value: 'staging', label: 'staging' },
    { value: 'prod', label: 'prod' },
  ];
  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly registries = signal<readonly RegistryResponse[]>([]);
  readonly registryKind = signal('Harbor');
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
    this.form.controls.kind.valueChanges.pipe(takeUntilDestroyed()).subscribe((kind) => this.registryKind.set(kind));
    void this.load();
  }

  isBusy(key: string): boolean {
    return this.busy().has(key);
  }

  fieldError(control: AbstractControl, messages: Record<string, string>): string {
    return controlError(control, messages);
  }

  async load(): Promise<void> {
    await runLoad(this.status, this.refreshing, this.refreshError, async () => {
      const response = await firstValueFrom(this.http.get<RegistryListResponse>(`${environment.apiUrl}/registries`));
      this.registries.set(response.registries);
    });
  }

  async save(): Promise<void> {
    this.feedback.clear();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid([
        { control: this.form.controls.name, id: 'registry-name' },
        { control: this.form.controls.kind, id: 'registry-kind' },
        { control: this.form.controls.server, id: 'registry-server' },
      ]);
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
