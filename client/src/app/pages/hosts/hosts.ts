import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { HostListResponse, HostSummary } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { runLoad } from '../../core/load-state';
import { problemMessage } from '../../core/problem-message';
import { ActionCluster } from '../../shared/action-cluster';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { TextField } from '../../shared/text-field';
import { PageState } from '../../shared/page-state';
import { RecordList, RecordRow } from '../../shared/record-list';

@Component({
  selector: 'app-hosts',
  imports: [ReactiveFormsModule, MatButtonModule, TextField, PageState, RecordList, RecordRow, ActionCluster],
  templateUrl: './hosts.html',
})
export class Hosts {
  private readonly http = inject(HttpClient);
  private readonly feedback = inject(FeedbackService);
  private readonly confirm = inject(ConfirmService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly hosts = signal<readonly HostSummary[]>([]);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    endpoint: new FormControl('unix:///var/run/docker.sock', { nonNullable: true, validators: [Validators.required] }),
    clientCertRef: new FormControl('', { nonNullable: true }),
    clientKeyRef: new FormControl('', { nonNullable: true }),
    caRef: new FormControl('', { nonNullable: true }),
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
      const response = await firstValueFrom(this.http.get<HostListResponse>(`${environment.apiUrl}/platform/hosts`));
      this.hosts.set(response.hosts);
    });
  }

  async register(): Promise<void> {
    this.feedback.clear();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid([
        { control: this.form.controls.name, id: 'host-name' },
        { control: this.form.controls.endpoint, id: 'host-endpoint' },
      ]);
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
