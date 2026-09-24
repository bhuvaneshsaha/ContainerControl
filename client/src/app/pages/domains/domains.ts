import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { DomainListResponse, DomainResponse } from '../../core/api-models';

@Component({
  selector: 'app-domains',
  imports: [ReactiveFormsModule],
  templateUrl: './domains.html',
})
export class Domains {
  private readonly http = inject(HttpClient);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
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
    this.message.set('');
    if (this.form.invalid) {
      this.message.set('Enter a domain name.');
      return;
    }

    try {
      await firstValueFrom(
        this.http.post(`${environment.apiUrl}/edge/domains`, this.form.getRawValue(), {
          observe: 'response',
          responseType: 'text',
        }),
      );
      this.form.controls.name.setValue('');
      await this.load();
    } catch {
      this.message.set('The domain could not be saved.');
    }
  }
}
