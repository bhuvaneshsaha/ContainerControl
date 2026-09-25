import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { runLoad } from '../../core/load-state';
import { problemMessage } from '../../core/problem-message';
import { PageState } from '../../shared/page-state';
import { TextField } from '../../shared/text-field';

interface AlertSettings {
  webhookUrl: string | null;
  recipients: string | null;
  smtpConfigured: boolean;
}

@Component({
  selector: 'app-alerts',
  imports: [ReactiveFormsModule, MatButtonModule, TextField, PageState],
  templateUrl: './alerts.html',
})
export class Alerts {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly smtpConfigured = signal(false);
  readonly form = new FormGroup({
    webhookUrl: new FormControl('', { nonNullable: true }),
    recipients: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    void this.load();
  }

  isBusy(key: string): boolean {
    return this.busy().has(key);
  }

  async load(): Promise<void> {
    await runLoad(this.status, this.refreshing, this.refreshError, async () => {
      const response = await firstValueFrom(this.http.get<AlertSettings>(`${environment.apiUrl}/platform/alerts`));
      this.form.setValue({
        webhookUrl: response.webhookUrl ?? '',
        recipients: response.recipients ?? '',
      });
      this.smtpConfigured.set(response.smtpConfigured);
    });
  }

  async save(): Promise<void> {
    this.feedback.clear();
    const webhookUrl = this.form.controls.webhookUrl.value.trim();
    const recipients = this.form.controls.recipients.value.trim();
    await runBusy(this.busy, 'save', async () => {
      try {
        await firstValueFrom(
          this.http.put(`${environment.apiUrl}/platform/alerts`, {
            webhookUrl: webhookUrl.length === 0 ? null : webhookUrl,
            recipients: recipients.length === 0 ? null : recipients,
          }),
        );
        this.feedback.success('Alert settings were saved.');
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'Alert settings could not be saved.'));
      }
    });
  }
}
