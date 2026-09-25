import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { environment } from '../../../environments/environment';
import { SignIn } from './sign-in';

describe('SignIn', () => {
  let fixture: ComponentFixture<SignIn>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(SignIn);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('asks for an email and password', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Sign in');
    expect(compiled.querySelector('#email')).not.toBeNull();
    expect(compiled.querySelector('#password')).not.toBeNull();
    expect(compiled.textContent).toContain('Email');
    expect(compiled.textContent).toContain('Password');
  });

  it('shows a field error for email and password when the form is empty', async () => {
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    button.click();
    fixture.detectChanges();
    fixture.detectChanges();
    await fixture.whenStable();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Enter an email.');
    expect(text).toContain('Enter a password.');
    expect((fixture.nativeElement.querySelector('button') as HTMLButtonElement).textContent).toContain('Sign in');
  });

  it('opens applications after sign-in when the account can read them', async () => {
    const http = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    fixture.componentInstance.form.setValue({ email: 'developer@localhost', password: 'secret' });

    const pending = fixture.componentInstance.submit();
    const login = await nextRequest(http, `${environment.apiUrl}/auth/login`);
    login.flush({});
    const session = await nextRequest(http, `${environment.apiUrl}/auth/session`);
    session.flush({ signedIn: true, permissions: ['apps.read'] });
    await pending;

    expect(navigate).toHaveBeenCalledWith('/apps');
    http.verify();
  });
});

async function nextRequest(http: HttpTestingController, url: string) {
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
