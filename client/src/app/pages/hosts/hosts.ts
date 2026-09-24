import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { HostListResponse, HostSummary } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { problemMessage } from '../../core/problem-message';

@Component({
  selector: 'app-hosts',
  imports: [ReactiveFormsModule],
  templateUrl: './hosts.html',
  styleUrl: './hosts.css',
})
export class Hosts {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly busy = signal<string | null>(null);
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
    this.feedback.clear();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.feedback.error('Enter a host name and an Engine endpoint.');
      return;
    }

    const value = this.form.getRawValue();
    await runBusy(this.busy, 'register', async () => {
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/platform/hosts`, value));
        this.form.controls.name.setValue('');
        this.feedback.success(`${value.name.trim()} was registered.`);
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The Docker host could not be registered.'));
      }
    });
  }

  async ping(host: HostSummary): Promise<void> {
    await runBusy(this.busy, `ping:${host.id}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/platform/hosts/${host.id}/ping`, {}));
        await this.load();
        const refreshed = this.hosts().find((item) => item.id === host.id);
        const version = refreshed?.engineVersion;
        this.feedback.success(version ? `Engine answered; version ${version}.` : 'Engine answered.');
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The Engine did not answer the version ping.'));
      }
    });
  }

  async prepare(host: HostSummary): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: `Prepare ${host.name}?`,
      body: `This creates the edge network and starts Traefik on ${host.name}. Do not prepare a host that already runs Traefik another way; both use ports 80 and 443.`,
      confirmLabel: 'Prepare edge',
    });
    if (!confirmed) {
      return;
    }

    await runBusy(this.busy, `prepare:${host.id}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(
          this.http.post(`${environment.apiUrl}/platform/hosts/${host.id}/prepare`, {}, {
            observe: 'response',
            responseType: 'text',
          }),
        );
        this.feedback.success('The edge network and Traefik container are ready.');
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The Docker host could not be prepared.'));
      }
    });
  }
}
