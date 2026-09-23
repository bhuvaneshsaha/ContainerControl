import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { Secrets } from './secrets';

describe('Secrets', () => {
  let fixture: ComponentFixture<Secrets>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Secrets],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Secrets);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/access/teams`).flush({ teams: [{ id: 'team-1', name: 'Product' }] });
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('lists the secret name and hides the value', async () => {
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.form.controls.teamId.setValue('team-1');
    fixture.componentInstance.form.controls.name.setValue('db-password');
    fixture.componentInstance.form.controls.value.setValue('super-secret-value');
    const pending = fixture.componentInstance.save();
    const save = http.expectOne(`${environment.apiUrl}/secrets`);
    expect(save.request.body.value).toBe('super-secret-value');
    save.flush('', { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    const list = http.expectOne(`${environment.apiUrl}/secrets?teamId=team-1&environment=dev`);
    list.flush({
      secrets: [{ id: '1', name: 'db-password', environment: 'dev', injectionMode: 'env', path: '/teams/team-1/db-password' }],
    });
    await pending;
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('db-password');
    expect(text).not.toContain('super-secret-value');
  });
});
