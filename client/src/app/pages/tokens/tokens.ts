import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { problemMessage } from '../../core/problem-message';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { TextField } from '../../shared/text-field';

@Component({
  selector: 'app-tokens',
  imports: [ReactiveFormsModule, MatButtonModule, TextField],
  templateUrl: './tokens.html',
})
export class Tokens {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);

  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly issued = signal<{ id: string; token: string } | null>(null);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  isBusy(key: string): boolean {
    return this.busy().has(key);
  }

  fieldError(control: AbstractControl, messages: Record<string, string>): string {
    return controlError(control, messages);
  }

  async issue(): Promise<void> {
    this.feedback.clear();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid([{ control: this.form.controls.name, id: 'token-name' }]);
      return;
    }

    await runBusy(this.busy, 'issue', async () => {
      try {
        const response = await firstValueFrom(
          this.http.post<{ id: string; token: string }>(`${environment.apiUrl}/access/tokens`, this.form.getRawValue()),
        );
        this.issued.set(response);
        this.form.controls.name.setValue('');
        this.form.markAsUntouched();
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
