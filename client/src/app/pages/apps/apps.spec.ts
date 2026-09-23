import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
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
  });
});
