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
    csrf.flush(null, { headers: { 'X-XSRF-TOKEN': 'token-1' } });

    const post = http.expectOne(`${environment.apiUrl}/platform/hosts`);
    expect(post.request.headers.get('X-XSRF-TOKEN')).toBe('token-1');
    post.flush({ id: 'host-1' });

    await pending;
  });
});
