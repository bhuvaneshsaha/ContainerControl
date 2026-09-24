import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { DomainListResponse, DomainResponse } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { problemMessage } from '../../core/problem-message';

@Component({
  selector: 'app-domains',
  imports: [ReactiveFormsModule],
  templateUrl: './domains.html',
})
export class Domains {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly busy = signal<string | null>(null);
  readonly domains = signal<readonly DomainResponse[]>([]);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const response = await firstValueFrom(this.http.get<DomainListResponse>(`${environment.apiUrl}/edge/domains`));
      this.domains.set(response.domains);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  async save(): Promise<void> {
    this.feedback.clear();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.feedback.error('Enter a domain name.');
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
        this.feedback.success(`${name} was saved.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The domain could not be saved.'));
      }
    });
  }
}
