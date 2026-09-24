import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { HostListResponse, HostSummary } from '../../core/api-models';
import { problemMessage } from '../../core/problem-message';

@Component({
  selector: 'app-hosts',
  imports: [ReactiveFormsModule],
  templateUrl: './hosts.html',
  styleUrl: './hosts.css',
})
export class Hosts {
  private readonly http = inject(HttpClient);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
  readonly hosts = signal<readonly HostSummary[]>([]);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    endpoint: new FormControl('unix:///var/run/docker.sock', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const response = await firstValueFrom(this.http.get<HostListResponse>(`${environment.apiUrl}/platform/hosts`));
      this.hosts.set(response.hosts);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  async register(): Promise<void> {
    this.message.set('');
    if (this.form.invalid) {
      this.message.set('Enter a host name and an Engine endpoint.');
      return;
    }

    try {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/platform/hosts`, this.form.getRawValue()));
      this.form.controls.name.setValue('');
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The Docker host could not be registered.'));
    }
  }

  async ping(host: HostSummary): Promise<void> {
    this.message.set('');
    try {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/platform/hosts/${host.id}/ping`, {}));
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The Engine did not answer the version ping.'));
    }
  }

  async prepare(host: HostSummary): Promise<void> {
    this.message.set('');
    try {
      await firstValueFrom(
        this.http.post(`${environment.apiUrl}/platform/hosts/${host.id}/prepare`, {}, {
          observe: 'response',
          responseType: 'text',
        }),
      );
      this.message.set('The edge network and Traefik container are ready.');
    } catch (error) {
      this.message.set(problemMessage(error, 'The Docker host could not be prepared.'));
    }
  }
}
