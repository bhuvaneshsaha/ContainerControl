import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { PermissionService } from '../../core/permissions';
import { Capacity } from './capacity';

describe('Capacity', () => {
  let fixture: ComponentFixture<Capacity>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Capacity],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    TestBed.inject(PermissionService).setPermissions(['platform.quotas.manage', 'platform.capacity.read']);
    fixture = TestBed.createComponent(Capacity);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/platform/quotas`).flush({ quotas: [] });
    http.expectOne(`${environment.apiUrl}/access/teams`).flush({ teams: [{ id: 'team-1', name: 'Platform' }] });
    await fixture.whenStable();
    http.expectOne(`${environment.apiUrl}/platform/capacity`).flush({
      hosts: [{ hostId: 'h1', hostName: 'edge', cpuCount: 4, memoryBytes: 8589934592, storageBytes: 107374182400, readAtUtc: '2026-09-24T00:00:00Z' }],
    });
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('edits team CPU, memory, and storage and shows the capacity table', () => {
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('#quota-cpu')).not.toBeNull();
    expect(page.querySelector('#quota-memory')).not.toBeNull();
    expect(page.querySelector('#quota-storage')).not.toBeNull();
    expect(page.querySelector('table')?.textContent).toContain('edge');
    expect(page.textContent).toContain('Storage');
  });
});
