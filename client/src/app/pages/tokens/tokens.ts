import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { problemMessage } from '../../core/problem-message';

@Component({
  selector: 'app-tokens',
  imports: [ReactiveFormsModule],
  templateUrl: './tokens.html',
})
export class Tokens {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);

  readonly busy = signal<string | null>(null);
  readonly issued = signal<{ id: string; token: string } | null>(null);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  async issue(): Promise<void> {
    this.feedback.clear();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.feedback.error('Enter a token name.');
      return;
    }

    await runBusy(this.busy, 'issue', async () => {
      try {
        const response = await firstValueFrom(
          this.http.post<{ id: string; token: string }>(`${environment.apiUrl}/access/tokens`, this.form.getRawValue()),
        );
        this.issued.set(response);
        this.form.controls.name.setValue('');
      } catch (error) {
        this.issued.set(null);
        this.feedback.error(problemMessage(error, 'The API token could not be issued.'));
      }
    });
  }

  async copy(): Promise<void> {
    const current = this.issued();
    if (!current) {
      return;
    }

    try {
      await navigator.clipboard.writeText(current.token);
    } catch {
      this.feedback.error('The token could not be copied. Select it and copy it manually, then dismiss this panel.');
      return;
    }

    this.issued.set(null);
    this.feedback.success('Token copied. It will not be shown again.');
  }

  dismissIssued(): void {
    this.issued.set(null);
    this.feedback.success('Token hidden. It will not be shown again.');
  }
}
