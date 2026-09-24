import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { problemMessage } from '../../core/problem-message';
import { PermissionService } from '../../core/permissions';
import { HasPermission } from '../../shared/has-permission';

interface QuotaResponse {
  teamId: string;
  cpuMillicores: number;
  memoryBytes: number;
  storageBytes: number;
}

interface CapacityRow {
  hostId: string;
  hostName: string;
  cpuCount: number | null;
  memoryBytes: number | null;
  storageBytes: number | null;
  readAtUtc: string | null;
}

@Component({
  selector: 'app-capacity',
  imports: [ReactiveFormsModule, HasPermission],
  templateUrl: './capacity.html',
})
export class Capacity {
  private readonly http = inject(HttpClient);
  private readonly permissions = inject(PermissionService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly message = signal('');
  readonly quotas = signal<readonly QuotaResponse[]>([]);
  readonly hosts = signal<readonly CapacityRow[]>([]);
  readonly form = new FormGroup({
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    cpu: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.001)] }),
    memoryGiB: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.001)] }),
    storageGiB: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.001)] }),
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      if (this.permissions.hasPermission('platform.quotas.manage')) {
        const response = await firstValueFrom(
          this.http.get<{ quotas: QuotaResponse[] }>(`${environment.apiUrl}/platform/quotas`),
        );
        this.quotas.set(response.quotas);
      }

      if (this.permissions.hasPermission('platform.capacity.read')) {
        const response = await firstValueFrom(
          this.http.get<{ hosts: CapacityRow[] }>(`${environment.apiUrl}/platform/capacity`),
        );
        this.hosts.set(response.hosts);
      }

      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  async save(): Promise<void> {
    this.message.set('');
    if (this.form.invalid) {
      this.message.set('Enter a team and a CPU, memory, and storage quota.');
      return;
    }

    const value = this.form.getRawValue();
    const gib = 1024 * 1024 * 1024;
    try {
      await firstValueFrom(
        this.http.put(`${environment.apiUrl}/platform/quotas/${value.teamId}`, {
          cpuMillicores: Math.round(value.cpu * 1000),
          memoryBytes: Math.round(value.memoryGiB * gib),
          storageBytes: Math.round(value.storageGiB * gib),
        }),
      );
      this.message.set('The team quota was saved.');
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The quota could not be saved.'));
    }
  }

  async read(host: CapacityRow): Promise<void> {
    this.message.set('');
    try {
      await firstValueFrom(this.http.post(`${environment.apiUrl}/platform/capacity/${host.hostId}`, {}));
      this.message.set('Host capacity was read from the Engine.');
      await this.load();
    } catch (error) {
      this.message.set(problemMessage(error, 'The Docker host could not be read.'));
    }
  }

  cpuLabel(millicores: number): string {
    return `${millicores / 1000} CPU`;
  }

  bytesLabel(bytes: number | null): string {
    if (bytes === null) {
      return 'Not reported';
    }

    return `${bytes / (1024 * 1024 * 1024)} GiB`;
  }
}
