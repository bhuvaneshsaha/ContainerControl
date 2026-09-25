import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { FeedbackService } from '../../core/feedback';
import { Alerts } from './alerts';

describe('Alerts', () => {
  let fixture: ComponentFixture<Alerts>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Alerts],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(Alerts);
  });

  it('saves a webhook and recipients without an SMTP password', async () => {
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/platform/alerts`).flush({
      webhookUrl: null,
      recipients: null,
      smtpConfigured: false,
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('SMTP is not configured');
    fixture.componentInstance.form.setValue({
      webhookUrl: 'https://hooks.example.com/alerts',
      recipients: 'ops@example.com',
    });

    const pending = fixture.componentInstance.save();
    const save = http.expectOne(`${environment.apiUrl}/platform/alerts`);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({
      webhookUrl: 'https://hooks.example.com/alerts',
      recipients: 'ops@example.com',
    });
    expect(JSON.stringify(save.request.body)).not.toContain('password');
    save.flush(null);
    await fixture.whenStable();
    http.expectOne(`${environment.apiUrl}/platform/alerts`).flush({
      webhookUrl: 'https://hooks.example.com/alerts',
      recipients: 'ops@example.com',
      smtpConfigured: true,
    });
    await pending;

    expect(TestBed.inject(FeedbackService).items()[0].text).toBe('Alert settings were saved.');
    expect(fixture.nativeElement.textContent).toContain('SMTP is configured');
  });
});
