import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { AppResponse } from '../../core/api-models';
import { Apps } from './apps';

describe('Apps', () => {
  let fixture: ComponentFixture<Apps>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Apps],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Apps);
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
    };

    const pending = fixture.componentInstance.act(app, 'deploy');
    http
      .expectOne(`${environment.apiUrl}/apps/${app.id}/deploy`)
      .flush(
        { title: "The hostname 'nginx.apps.example.com' is not under an allowed domain." },
        { status: 400, statusText: 'Bad Request' },
      );
    await pending;

    expect(fixture.componentInstance.message()).toBe(
      "The hostname 'nginx.apps.example.com' is not under an allowed domain.",
    );
  });
});
