import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { DomainListResponse, DomainResponse } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { runLoad } from '../../core/load-state';
import { problemMessage } from '../../core/problem-message';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { TextField } from '../../shared/text-field';
import { PageState } from '../../shared/page-state';
import { RecordList, RecordRow } from '../../shared/record-list';

@Component({
  selector: 'app-domains',
  imports: [ReactiveFormsModule, MatButtonModule, TextField, PageState, RecordList, RecordRow],
  templateUrl: './domains.html',
})
export class Domains {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly domains = signal<readonly DomainResponse[]>([]);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
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

  async load(): Promise<void> {
    await runLoad(this.status, this.refreshing, this.refreshError, async () => {
      const response = await firstValueFrom(this.http.get<DomainListResponse>(`${environment.apiUrl}/edge/domains`));
      this.domains.set(response.domains);
    });
  }

  async save(): Promise<void> {
    this.feedback.clear();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid([{ control: this.form.controls.name, id: 'domain-name' }]);
      return;
    }

    const name = this.form.controls.name.value.trim();
    await runBusy(this.busy, 'save', async () => {
      try {
        await firstValueFrom(
          this.http.post(`${environment.apiUrl}/edge/domains`, { name }, {
            observe: 'response',
            responseType: 'text',
          }),
        );
        this.form.controls.name.setValue('');
        this.form.markAsUntouched();
        this.feedback.success(`${name} was saved.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The domain could not be saved.'));
      }
    });
  }
}
