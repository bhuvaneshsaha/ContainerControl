import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { AppResponse } from '../../core/api-models';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { PermissionService } from '../../core/permissions';
import { Apps } from './apps';

describe('Apps', () => {
  let fixture: ComponentFixture<Apps>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Apps],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Apps);
    TestBed.inject(PermissionService).setPermissions([
      'apps.read',
      'apps.write',
      'deploy.execute',
      'deploy.rollback',
      'runtime.control',
      'runtime.logs.read',
      'runtime.stats.read',
      'secrets.read',
      'secrets.manage',
    ]);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/apps`).flush({ apps: [] });
    http.expectOne(`${environment.apiUrl}/access/teams`).flush({ teams: [] });
    http.expectOne(`${environment.apiUrl}/platform/hosts/choices`).flush({ hosts: [] });
    await fixture.whenStable();
  });

  it('shows one sentence when no applications exist', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No applications are registered yet.');
    expect(fixture.nativeElement.querySelector('#app-compose')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#app-image')).toBeNull();
  });

  it('shows the API reason when deploy is rejected', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'welcome',
      environment: 'dev',
      image: null,
      status: 'registered',
      hostname: 'http://nginx.apps.example.com/',
      exposed: false,
      requiresApproval: false,
    };

    const pending = fixture.componentInstance.act(app, 'deploy');
    http
      .expectOne(`${environment.apiUrl}/apps/${app.id}/deploy`)
      .flush(
        { title: "The hostname 'nginx.apps.example.com' is not under an allowed domain." },
        { status: 400, statusText: 'Bad Request' },
      );
    await pending;

    const notice = TestBed.inject(FeedbackService).items()[0];
    expect(notice.kind).toBe('alert');
    expect(notice.text).toBe("The hostname 'nginx.apps.example.com' is not under an allowed domain.");
  });

  it('hides create and runtime actions without those permissions', async () => {
    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([
      {
        id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
        teamId: 'team',
        hostId: 'host',
        name: 'welcome',
        environment: 'dev',
        image: null,
        status: 'registered',
        hostname: null,
        exposed: false,
        requiresApproval: false,
      },
    ]);
    TestBed.inject(PermissionService).setPermissions(['apps.read']);
    fixture.detectChanges();
    await fixture.whenStable();
    const text = fixture.nativeElement.textContent as string;
    expect(fixture.nativeElement.querySelector('#app-compose')).toBeNull();
    expect(text).not.toContain('Deploy');
    expect(text).not.toContain('Stop');
    expect(text).not.toContain('Rollback');
    expect(text).not.toContain('Secrets');
    expect(text).not.toContain('Stats');
  });

  it('does not stop an application until the operator confirms', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'welcome',
      environment: 'dev',
      image: null,
      status: 'running',
      hostname: null,
      exposed: false,
      requiresApproval: false,
    };

    const pending = fixture.componentInstance.act(app, 'stop');
    TestBed.inject(ConfirmService).answer(false);
    await pending;

    http.expectNone(`${environment.apiUrl}/apps/${app.id}/stop`);
  });

  it('shows Approve only for a pending app when the caller has deploy.approve', async () => {
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'welcome',
      environment: 'prod',
      image: null,
      status: 'pending-approval',
      hostname: null,
      exposed: false,
      requiresApproval: true,
    };
    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([app]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Approve');

    TestBed.inject(PermissionService).setPermissions(['deploy.approve']);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Approve');
  });
});
