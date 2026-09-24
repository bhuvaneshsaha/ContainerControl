import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { Permissions } from './permissions';

describe('Permissions', () => {
  let fixture: ComponentFixture<Permissions>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Permissions],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Permissions);
    http = TestBed.inject(HttpTestingController);
  });

  it('lists permission codes returned by the API', async () => {
    http.expectOne(`${environment.apiUrl}/me/permissions`).flush({ permissions: ['apps.read'] });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('apps.read');
  });

  it('shows one sentence when the account has no permissions', async () => {
    http.expectOne(`${environment.apiUrl}/me/permissions`).flush({ permissions: [] });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('This account has no permissions.');
  });

  it('shows one sentence when permissions cannot be loaded', async () => {
    http.expectOne(`${environment.apiUrl}/me/permissions`).flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Permissions could not be loaded.');
  });
});
