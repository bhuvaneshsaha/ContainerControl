import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { problemMessage } from '../../core/problem-message';

@Component({
  selector: 'app-tokens',
  imports: [ReactiveFormsModule],
  templateUrl: './tokens.html',
})
export class Tokens {
  private readonly http = inject(HttpClient);

  readonly message = signal('');
  readonly token = signal('');
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  async issue(): Promise<void> {
    this.message.set('');
    this.token.set('');
    if (this.form.invalid) {
      this.message.set('Enter a token name.');
      return;
    }

    try {
      const response = await firstValueFrom(
        this.http.post<{ id: string; token: string }>(`${environment.apiUrl}/access/tokens`, this.form.getRawValue()),
      );
      this.token.set(response.token);
      this.form.controls.name.setValue('');
    } catch (error) {
      this.message.set(problemMessage(error, 'The API token could not be issued.'));
    }
  }
}
