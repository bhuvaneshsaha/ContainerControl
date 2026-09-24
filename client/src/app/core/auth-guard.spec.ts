import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';

import { environment } from '../../environments/environment';
import { authGuard } from './auth-guard';

describe('authGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('redirects to sign-in after an anonymous session without leaving the injection context', async () => {
    const http = TestBed.inject(HttpTestingController);
    const pending = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    http.expectOne(`${environment.apiUrl}/auth/session`).flush({ signedIn: false, permissions: [] });

    const outcome = await pending;
    expect(outcome).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(outcome as UrlTree)).toBe('/sign-in');
    http.verify();
  });
});
