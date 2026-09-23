import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { AuthService } from './auth';
import { credentialsInterceptor } from './credentials-interceptor';
import { PermissionService } from './permissions';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([credentialsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('stores permissions after a cookie sign-in', async () => {
    const pending = service.signIn('developer@localhost', 'secret');

    const csrf = await nextRequest(`${environment.apiUrl}/auth/csrf`);
    expect(csrf.request.withCredentials).toBe(true);
    csrf.flush(null);

    const login = await nextRequest(`${environment.apiUrl}/auth/login`);
    expect(login.request.withCredentials).toBe(true);
    login.flush(null);

    const permissions = await nextRequest(`${environment.apiUrl}/me/permissions`);
    permissions.flush({ permissions: ['apps.read'] });

    await pending;

    expect(service.signedIn()).toBe(true);
    expect(TestBed.inject(PermissionService).hasPermission('apps.read')).toBe(true);
    expect(TestBed.inject(PermissionService).hasPermission('platform.hosts.manage')).toBe(false);
  });

  async function nextRequest(url: string) {
    const started = Date.now();
    while (Date.now() - started < 1000) {
      const matches = http.match(url);
      if (matches.length === 1) {
        return matches[0];
      }
      await new Promise((resolve) => setTimeout(resolve, 0));
    }

    throw new Error(`Timed out waiting for ${url}`);
  }
});
