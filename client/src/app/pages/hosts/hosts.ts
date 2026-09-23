import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { HostListResponse, HostSummary } from '../../core/api-models';

@Component({
  selector: 'app-hosts',
  imports: [],
  templateUrl: './hosts.html',
  styleUrl: './hosts.css',
})
export class Hosts {
  private readonly http = inject(HttpClient);

  readonly status = signal<'loading' | 'ready' | 'error'>('loading');
  readonly hosts = signal<readonly HostSummary[]>([]);

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const response = await firstValueFrom(
        this.http.get<HostListResponse>(`${environment.apiUrl}/platform/hosts`),
      );
      this.hosts.set(response.hosts);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }
}
