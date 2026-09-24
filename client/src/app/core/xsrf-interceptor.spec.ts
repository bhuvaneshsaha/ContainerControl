import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
import { xsrfInterceptor } from './xsrf-interceptor';

describe('xsrfInterceptor', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([xsrfInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('requests a token before a post when the page was reloaded', async () => {
    const pending = firstValueFrom(
      TestBed.inject(HttpClient).post(`${environment.apiUrl}/platform/hosts`, {
        name: 'local',
        endpoint: 'npipe://./pipe/docker_engine',
      }),
    );

    const csrf = http.expectOne(`${environment.apiUrl}/auth/csrf`);
    expect(csrf.request.withCredentials).toBe(true);
    csrf.flush({ token: 'token-1' });

    const post = http.expectOne(`${environment.apiUrl}/platform/hosts`);
    expect(post.request.headers.get('X-XSRF-TOKEN')).toBe('token-1');
    post.flush({ id: 'host-1' });

    await pending;
  });

  it('asks for a new token after sign-in instead of reusing the anonymous one', async () => {
    const client = TestBed.inject(HttpClient);
    const first = firstValueFrom(client.post(`${environment.apiUrl}/auth/login`, {}));
    http.expectOne(`${environment.apiUrl}/auth/csrf`).flush({ token: 'anonymous' });
    http.expectOne(`${environment.apiUrl}/auth/login`).flush(null);
    await first;

    const second = firstValueFrom(client.post(`${environment.apiUrl}/platform/hosts`, { name: 'local' }));
    http.expectOne(`${environment.apiUrl}/auth/csrf`).flush({ token: 'signed-in' });
    const post = http.expectOne(`${environment.apiUrl}/platform/hosts`);
    expect(post.request.headers.get('X-XSRF-TOKEN')).toBe('signed-in');
    post.flush({ id: 'host-1' });
    await second;
  });
});
