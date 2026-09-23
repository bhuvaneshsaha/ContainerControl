import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { environment } from '../environments/environment';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('shows the product name and hides host navigation without that permission', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/auth/session`).flush({ signedIn: true, permissions: ['apps.read'] });
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.product')?.textContent).toContain('ContainerControl');
    expect(compiled.textContent).toContain('Permissions');
    expect(compiled.textContent).not.toContain('Hosts');
    expect(compiled.querySelector('main')).not.toBeNull();
  });
});
