import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

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
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
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
      allowDatabaseImages: false,
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
        allowDatabaseImages: false,
      },
    ]);
    TestBed.inject(PermissionService).setPermissions(['apps.read']);
    fixture.detectChanges();
    await fixture.whenStable();
    const text = fixture.nativeElement.textContent as string;
    expect(fixture.nativeElement.querySelector('#app-compose')).toBeNull();
    const buttons = [...fixture.nativeElement.querySelectorAll('button')].map((button) => button.textContent ?? '');
    expect(text).not.toContain('Edit');
    expect(text).not.toContain('Remove');
    expect(buttons.some((label) => label.includes('Deploy'))).toBe(false);
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
      allowDatabaseImages: false,
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
      allowDatabaseImages: false,
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

  it('hides the database-image override unless the caller can manage platform settings', async () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#app-database')).toBeNull();

    TestBed.inject(PermissionService).setPermissions(['apps.write', 'platform.settings.manage']);
    fixture.detectChanges();
    await fixture.whenStable();
    const box = fixture.nativeElement.querySelector('#app-database-input') as HTMLInputElement;
    expect(box).not.toBeNull();
    expect(box.checked).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('app host');
  });

  it('sends the database-image override only when the operator turns it on', async () => {
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(PermissionService).setPermissions(['apps.write', 'platform.settings.manage']);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.form.setValue({
      name: 'billing',
      teamId: 'team',
      hostId: 'host',
      environment: 'dev',
      composeYaml: 'services:\n  web:\n    image: nginx:1.27\n',
      internalPort: '',
      hostname: '',
      exposed: false,
      requireApproval: false,
      allowDatabaseImages: true,
    });

    const pending = fixture.componentInstance.create();
    const request = http.expectOne(`${environment.apiUrl}/apps`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.allowDatabaseImages).toBe(true);
    request.flush({ id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636' });
    await fixture.whenStable();
    http.expectOne(`${environment.apiUrl}/apps`).flush({ apps: [] });
    http.expectOne(`${environment.apiUrl}/access/teams`).flush({ teams: [] });
    http.expectOne(`${environment.apiUrl}/platform/hosts/choices`).flush({ hosts: [] });
    await pending;
  });

  it('shows when an application is allowed to run database images', () => {
    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([
      {
        id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
        teamId: 'team',
        hostId: 'host',
        name: 'ledger',
        environment: 'dev',
        image: null,
        status: 'registered',
        hostname: null,
        exposed: false,
        requiresApproval: false,
        allowDatabaseImages: true,
      },
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Database images allowed');
  });

  it('shows service targets and posts only the selected services', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'billing',
      environment: 'dev',
      image: null,
      composeYaml: 'services:\n  api:\n    image: nginx:1.27\n  worker:\n    image: busybox:1.36.1\n',
      status: 'registered',
      hostname: null,
      exposed: false,
      requiresApproval: false,
      allowDatabaseImages: false,
    };

    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([app]);
    const pending = fixture.componentInstance.openSecrets(app);
    await Promise.resolve();
    http.expectOne(`${environment.apiUrl}/secrets?teamId=team&environment=dev`).flush({
      secrets: [
        {
          id: 'secret-1',
          name: 'DB_PASSWORD',
          environment: 'dev',
          injectionMode: 'env',
          path: '/teams/team/DB_PASSWORD',
          serviceNames: ['api', 'cron'],
        },
      ],
    });
    await pending;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('DB_PASSWORD (env) → api, cron');
    const api = fixture.nativeElement.querySelector('#secret-service-api-input') as HTMLInputElement;
    const worker = fixture.nativeElement.querySelector('#secret-service-worker-input') as HTMLInputElement;
    expect(api.checked).toBe(false);
    expect(worker.checked).toBe(false);

    worker.click();
    fixture.detectChanges();
    fixture.componentInstance.secretForm.setValue({
      name: 'DB_PASSWORD',
      injectionMode: 'env',
      value: 'stored-elsewhere',
      serviceNames: fixture.componentInstance.secretForm.controls.serviceNames.value,
    });

    const save = fixture.componentInstance.saveSecret();
    const request = http.expectOne(`${environment.apiUrl}/secrets`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.serviceNames).toEqual(['cron', 'worker']);
    expect(request.request.body.value).toBe('stored-elsewhere');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    http.expectOne(`${environment.apiUrl}/secrets?teamId=team&environment=dev`).flush({ secrets: [] });
    await save;
  });

  it('selects the only service for a single-container app', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'welcome',
      environment: 'dev',
      image: 'nginx:1.27',
      composeYaml: null,
      status: 'registered',
      hostname: null,
      exposed: false,
      requiresApproval: false,
      allowDatabaseImages: false,
    };

    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([app]);
    const pending = fixture.componentInstance.openSecrets(app);
    await Promise.resolve();
    http.expectOne(`${environment.apiUrl}/secrets?teamId=team&environment=dev`).flush({ secrets: [] });
    await pending;
    fixture.detectChanges();

    const box = fixture.nativeElement.querySelector('#secret-service-app-input') as HTMLInputElement;
    expect(box.checked).toBe(true);
    expect(fixture.nativeElement.textContent).not.toContain('This application has no services to assign.');
  });

  it('does not save a secret when no service is selected', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'billing',
      environment: 'dev',
      image: null,
      composeYaml: 'services:\n  api:\n    image: nginx:1.27\n  worker:\n    image: busybox:1.36.1\n',
      status: 'registered',
      hostname: null,
      exposed: false,
      requiresApproval: false,
      allowDatabaseImages: false,
    };

    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([app]);
    const opened = fixture.componentInstance.openSecrets(app);
    await Promise.resolve();
    http.expectOne(`${environment.apiUrl}/secrets?teamId=team&environment=dev`).flush({ secrets: [] });
    await opened;
    fixture.componentInstance.secretForm.setValue({
      name: 'DB_PASSWORD',
      injectionMode: 'env',
      value: 'stored-elsewhere',
      serviceNames: [],
    });

    await fixture.componentInstance.saveSecret();
    fixture.detectChanges();
    http.expectNone(`${environment.apiUrl}/secrets`);
    expect(fixture.nativeElement.textContent).toContain('Select at least one service for this secret.');
    expect(TestBed.inject(FeedbackService).items()).toHaveLength(0);
  });

  it('prefills the edit form and sends name and host without team or environment', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'welcome',
      environment: 'dev',
      image: 'nginx:1.27',
      composeYaml: null,
      command: ['nginx', '-g', 'daemon off;'],
      internalPort: 80,
      status: 'registered',
      hostname: 'web.apps.localhost',
      exposed: true,
      requiresApproval: false,
      allowDatabaseImages: false,
    };
    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([app]);
    fixture.componentInstance.teams.set([{ id: 'team', name: 'Platform' }]);
    fixture.componentInstance.hosts.set([
      { id: 'host', name: 'local' },
      { id: 'host-2', name: 'other' },
    ]);
    fixture.detectChanges();

    await fixture.componentInstance.openEdit(app);
    fixture.detectChanges();
    const name = fixture.nativeElement.querySelector('#edit-app-name') as HTMLInputElement;
    const image = fixture.nativeElement.querySelector('#edit-app-image') as HTMLInputElement;
    const port = fixture.nativeElement.querySelector('#edit-app-port') as HTMLInputElement;
    expect(name.value).toBe('welcome');
    expect(image.value).toBe('nginx:1.27');
    expect(port.value).toBe('80');
    expect(fixture.nativeElement.querySelector('#edit-app-team').textContent).toContain('Platform');
    expect(fixture.nativeElement.querySelector('#edit-app-environment').textContent).toContain('dev');
    expect(fixture.nativeElement.querySelector('#edit-app-environment-input')).toBeNull();

    fixture.componentInstance.editForm.controls.name.setValue('welcome-renamed');
    fixture.componentInstance.editForm.controls.hostId.setValue('host-2');
    const pending = fixture.componentInstance.saveEdit();
    const request = http.expectOne(`${environment.apiUrl}/apps/${app.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.name).toBe('welcome-renamed');
    expect(request.request.body.hostId).toBe('host-2');
    expect(request.request.body.teamId).toBeUndefined();
    expect(request.request.body.environment).toBeUndefined();
    expect(request.request.body.allowDatabaseImages).toBeUndefined();
    expect(request.request.body.command).toEqual(['nginx', '-g', 'daemon off;']);
    expect(request.request.body.internalPort).toBe(80);
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    http.expectOne(`${environment.apiUrl}/apps`).flush({
      apps: [{ ...app, name: 'welcome-renamed', hostId: 'host-2' }],
    });
    await pending;

    expect(fixture.componentInstance.editing()).toBeNull();
    expect(fixture.componentInstance.apps()[0].name).toBe('welcome-renamed');
    expect(TestBed.inject(FeedbackService).items()[0].kind).toBe('status');
    expect(TestBed.inject(FeedbackService).items()[0].text).toBe('welcome-renamed was saved.');
  });

  it('shows the API reason when an edit is rejected', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
      id: '97f94c8a-3d8e-49ee-9397-2a358eb9a636',
      teamId: 'team',
      hostId: 'host',
      name: 'welcome',
      environment: 'dev',
      image: null,
      composeYaml: 'services:\n  web:\n    image: nginx:1.27\n',
      status: 'registered',
      hostname: null,
      exposed: false,
      requiresApproval: false,
      allowDatabaseImages: false,
    };
    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([app]);
    await fixture.componentInstance.openEdit(app);
    fixture.detectChanges();
    fixture.componentInstance.editForm.controls.name.setValue('   ');
    await fixture.componentInstance.saveEdit();
    fixture.detectChanges();
    http.expectNone(`${environment.apiUrl}/apps/${app.id}`);
    expect(TestBed.inject(FeedbackService).items()).toHaveLength(0);
    expect(fixture.nativeElement.textContent).toContain('Enter a name.');

    fixture.componentInstance.editForm.controls.name.setValue('welcome');
    fixture.componentInstance.editForm.controls.composeYaml.setValue('');
    fixture.componentInstance.editForm.controls.image.setValue('');
    await fixture.componentInstance.saveEdit();
    fixture.detectChanges();
    http.expectNone(`${environment.apiUrl}/apps/${app.id}`);
    expect(fixture.nativeElement.textContent).toContain('Enter an image or a compose file.');

    TestBed.inject(FeedbackService).clear();
    fixture.componentInstance.editForm.controls.composeYaml.setValue('services:\n  web:\n    image: nginx:1.27\n');
    const pending = fixture.componentInstance.saveEdit();
    http.expectOne(`${environment.apiUrl}/apps/${app.id}`).flush(
      { errors: { app: ['An application with that name already exists in this team and environment.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await pending;
    expect(fixture.componentInstance.editing()?.id).toBe(app.id);
    expect(TestBed.inject(FeedbackService).items()[0].text).toBe(
      'An application with that name already exists in this team and environment.',
    );
  });

  it('does not remove an application until the operator confirms', async () => {
    const http = TestBed.inject(HttpTestingController);
    const app: AppResponse = {
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
      allowDatabaseImages: false,
    };

    const pending = fixture.componentInstance.remove(app);
    TestBed.inject(ConfirmService).answer(false);
    await pending;

    http.expectNone(`${environment.apiUrl}/apps/${app.id}`);
  });

  it('removes the application row after delete succeeds', async () => {
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
      allowDatabaseImages: false,
    };
    fixture.componentInstance.status.set('ready');
    fixture.componentInstance.apps.set([app]);
    fixture.componentInstance.editing.set(app);
    fixture.detectChanges();

    const pending = fixture.componentInstance.remove(app);
    TestBed.inject(ConfirmService).answer(true);
    await Promise.resolve();
    const request = http.expectOne(`${environment.apiUrl}/apps/${app.id}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    http.expectOne(`${environment.apiUrl}/apps`).flush({ apps: [] });
    await pending;

    expect(fixture.componentInstance.apps()).toEqual([]);
    expect(fixture.componentInstance.editing()).toBeNull();
    expect(TestBed.inject(FeedbackService).items()[0].text).toBe('welcome was removed.');
  });
});
