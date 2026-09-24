import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';

import { environment } from '../../environments/environment';
import { AuthService } from './auth';
import { authGuard, permissionGuardAny } from './auth-guard';
import { FeedbackService } from './feedback';
import { PermissionService } from './permissions';

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

  it('sends a forbidden deep-link to the permitted home and names the missing permission', async () => {
    const auth = TestBed.inject(AuthService);
    auth.signedIn.set(true);
    TestBed.inject(PermissionService).setPermissions(['apps.read']);
    const guard = permissionGuardAny(['platform.hosts.manage']);

    const outcome = await TestBed.runInInjectionContext(() => guard({} as never, {} as never));

    expect(TestBed.inject(Router).serializeUrl(outcome as UrlTree)).toBe('/apps');
    const notice = TestBed.inject(FeedbackService).items()[0];
    expect(notice.kind).toBe('status');
    expect(notice.text).toContain('platform.hosts.manage');
    expect(notice.text).toContain('Manage Docker hosts');
  });
});
