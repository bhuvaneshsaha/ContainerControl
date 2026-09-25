import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { TeamListResponse } from '../../core/api-models';
import { runBusy } from '../../core/busy';
import { FeedbackService } from '../../core/feedback';
import { runLoad } from '../../core/load-state';
import { problemMessage } from '../../core/problem-message';
import { PermissionService } from '../../core/permissions';
import { controlError, focusFirstInvalid } from '../../shared/field-error';
import { formatTimestamp } from '../../shared/format-time';
import { HasPermission } from '../../shared/has-permission';
import { PageState } from '../../shared/page-state';
import { RecordList, RecordRow } from '../../shared/record-list';
import { SectionBlock } from '../../shared/section-block';
import { SelectOption, SelectField } from '../../shared/select-field';
import { TextField } from '../../shared/text-field';

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
  imports: [ReactiveFormsModule, HasPermission, MatButtonModule, TextField, SelectField, PageState, RecordList, RecordRow, SectionBlock],
  templateUrl: './capacity.html',
})
export class Capacity {
  private readonly http = inject(HttpClient);
  private readonly permissions = inject(PermissionService);
  private readonly feedback = inject(FeedbackService);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly refreshing = signal(false);
  readonly refreshError = signal(false);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly quotas = signal<readonly QuotaResponse[]>([]);
  readonly hosts = signal<readonly CapacityRow[]>([]);
  readonly teams = signal<{ id: string; name: string }[]>([]);
  readonly form = new FormGroup({
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    cpu: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.001)] }),
    memoryGiB: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.001)] }),
    storageGiB: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(0.001)] }),
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

  formatTime(value: string): string {
    return formatTimestamp(value);
  }

  teamOptions(): SelectOption[] {
    return [{ value: '', label: 'Select a team' }, ...this.teams().map((team) => ({ value: team.id, label: team.name }))];
  }

  teamLabel(teamId: string): string {
    return this.teams().find((team) => team.id === teamId)?.name ?? teamId;
  }

  async load(): Promise<void> {
    await runLoad(this.status, this.refreshing, this.refreshError, async () => {
      if (this.permissions.hasPermission('platform.quotas.manage')) {
        const [quotas, teams] = await Promise.all([
          firstValueFrom(this.http.get<{ quotas: QuotaResponse[] }>(`${environment.apiUrl}/platform/quotas`)),
          firstValueFrom(this.http.get<TeamListResponse>(`${environment.apiUrl}/access/teams`)),
        ]);
        this.quotas.set(quotas.quotas);
        this.teams.set(teams.teams);
      }

      if (this.permissions.hasPermission('platform.capacity.read')) {
        const response = await firstValueFrom(
          this.http.get<{ hosts: CapacityRow[] }>(`${environment.apiUrl}/platform/capacity`),
        );
        this.hosts.set(response.hosts);
      }
    });
  }

  async save(): Promise<void> {
    this.feedback.clear();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      focusFirstInvalid([
        { control: this.form.controls.teamId, id: 'quota-team' },
        { control: this.form.controls.cpu, id: 'quota-cpu' },
        { control: this.form.controls.memoryGiB, id: 'quota-memory' },
        { control: this.form.controls.storageGiB, id: 'quota-storage' },
      ]);
      return;
    }

    const value = this.form.getRawValue();
    const gib = 1024 * 1024 * 1024;
    await runBusy(this.busy, 'save', async () => {
      try {
        await firstValueFrom(
          this.http.put(`${environment.apiUrl}/platform/quotas/${value.teamId}`, {
            cpuMillicores: Math.round(Number(value.cpu) * 1000),
            memoryBytes: Math.round(Number(value.memoryGiB) * gib),
            storageBytes: Math.round(Number(value.storageGiB) * gib),
          }),
        );
        this.feedback.success('The team quota was saved.');
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The quota could not be saved.'));
      }
    });
  }

  async read(host: CapacityRow): Promise<void> {
    await runBusy(this.busy, `read:${host.hostId}`, async () => {
      this.feedback.clear();
      try {
        await firstValueFrom(this.http.post(`${environment.apiUrl}/platform/capacity/${host.hostId}`, {}));
        this.feedback.success('Host capacity was read from the Engine.');
        await this.load();
      } catch (error) {
        this.feedback.error(problemMessage(error, 'The Docker host could not be read.'));
      }
    });
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
