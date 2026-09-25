import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { ConfirmService } from '../../core/confirm';
import { FeedbackService } from '../../core/feedback';
import { PermissionService } from '../../core/permissions';
import { Templates } from './templates';

const starter = {
  id: '4d6f8a10-2b3c-4d5e-8f70-112233445566',
  name: 'nginx',
  description: 'A web server.',
  composeYaml: 'services:\n  web:\n    image: nginx:stable\n',
};

describe('Templates', () => {
  let fixture: ComponentFixture<Templates>;

  async function setup(permissions: string[], templates = [starter]): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [Templates],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    TestBed.inject(PermissionService).setPermissions(permissions);
    fixture = TestBed.createComponent(Templates);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/templates`).flush({ templates });
    await Promise.resolve();
    if (permissions.includes('apps.write')) {
      http.expectOne(`${environment.apiUrl}/access/teams`).flush({ teams: [{ id: 'team-1', name: 'Product' }] });
      http.expectOne(`${environment.apiUrl}/platform/hosts/choices`).flush({ hosts: [{ id: 'host-1', name: 'local' }] });
    }
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('lets a developer create an application from a template without sending compose', async () => {
    await setup(['apps.read', 'apps.write']);
    expect(fixture.nativeElement.textContent).toContain('nginx');
    expect(fixture.nativeElement.querySelector('#template-compose')).toBeNull();
    expect(fixture.nativeElement.querySelector('#template-choice')).not.toBeNull();

    fixture.componentInstance.createForm.setValue({
      templateId: starter.id,
      name: 'welcome',
      teamId: 'team-1',
      hostId: 'host-1',
      environment: 'dev',
      internalPort: '80',
      hostname: 'web.apps.localhost',
      exposed: true,
      requireApproval: false,
    });

    const pending = fixture.componentInstance.create();
    const request = TestBed.inject(HttpTestingController).expectOne(`${environment.apiUrl}/apps/from-template`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      templateId: starter.id,
      teamId: 'team-1',
      hostId: 'host-1',
      name: 'welcome',
      environment: 'dev',
      internalPort: 80,
      hostname: 'web.apps.localhost',
      exposed: true,
      requiresApproval: false,
    });
    expect(JSON.stringify(request.request.body)).not.toContain('composeYaml');
    expect(JSON.stringify(request.request.body)).not.toContain('nginx:stable');
    request.flush({ id: 'app-1' });
    await pending;

    expect(TestBed.inject(FeedbackService).items()[0].text).toContain('welcome was created');
  });

  it('hides publish and remove without the template permission', async () => {
    await setup(['apps.read', 'apps.write']);
    expect(fixture.nativeElement.textContent).not.toContain('Publish template');
    expect(fixture.nativeElement.textContent).not.toContain('Remove');
  });

  it('publishes a template and does not echo a rejected secret value', async () => {
    await setup(['apps.templates.manage'], []);
    expect(fixture.nativeElement.textContent).toContain('No templates are published yet.');
    expect(fixture.nativeElement.querySelector('#template-compose')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#template-choice')).toBeNull();

    const secret = 'super-secret-value';
    fixture.componentInstance.publishForm.setValue({
      name: 'nginx',
      description: 'A web server.',
      composeYaml: `services:\n  web:\n    image: nginx:stable\n    environment:\n      API_TOKEN: ${secret}\n`,
    });

    const pending = fixture.componentInstance.publish();
    const request = TestBed.inject(HttpTestingController).expectOne(`${environment.apiUrl}/templates`);
    request.flush(
      { errors: { template: ['The template contains a secret value. Remove it and assign the secret on the application.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await pending;

    const notice = TestBed.inject(FeedbackService).items()[0];
    expect(notice.kind).toBe('alert');
    expect(notice.text).toContain('secret value');
    expect(notice.text).not.toContain(secret);
    expect(fixture.nativeElement.textContent).not.toContain(secret);
  });

  it('removes a template after confirmation', async () => {
    await setup(['apps.templates.manage']);
    const pending = fixture.componentInstance.remove(starter);
    TestBed.inject(ConfirmService).answer(true);
    await Promise.resolve();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/templates/${starter.id}`).flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    http.expectOne(`${environment.apiUrl}/templates`).flush({ templates: [] });
    await pending;
    expect(TestBed.inject(FeedbackService).items()[0].text).toContain('nginx was removed');
  });
});
