import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

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
    expect(compiled.querySelector('label[for="email"]')?.textContent).toContain('Email');
    expect(compiled.querySelector('label[for="password"]')?.textContent).toContain('Password');
  });

  it('shows one sentence when the form is empty', async () => {
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    button.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Enter an email and password.');
  });
});
