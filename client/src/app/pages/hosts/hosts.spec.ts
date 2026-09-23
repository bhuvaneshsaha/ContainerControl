import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { Hosts } from './hosts';

describe('Hosts', () => {
  let fixture: ComponentFixture<Hosts>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Hosts],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Hosts);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/platform/hosts`).flush({ hosts: [] });
    await fixture.whenStable();
  });

  it('shows one sentence when no hosts are registered', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No Docker hosts are registered yet.');
  });
});
