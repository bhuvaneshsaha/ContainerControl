import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { FeedbackService } from '../../core/feedback';
import { Tokens } from './tokens';

describe('Tokens', () => {
  let fixture: ComponentFixture<Tokens>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Tokens],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Tokens);
    fixture.detectChanges();
  });

  it('shows the issued token once and hides it after copy', async () => {
    const http = TestBed.inject(HttpTestingController);
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });
    fixture.componentInstance.form.controls.name.setValue('ci');

    const pending = fixture.componentInstance.issue();
    http.expectOne(`${environment.apiUrl}/access/tokens`).flush({ id: 'token-1', token: 'cc_secret' });
    await pending;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Copy this token now');
    expect(fixture.nativeElement.querySelector('#issued-token').value).toBe('cc_secret');

    await fixture.componentInstance.copy();
    fixture.detectChanges();

    expect(writeText).toHaveBeenCalledWith('cc_secret');
    expect(fixture.componentInstance.issued()).toBeNull();
    expect(TestBed.inject(FeedbackService).items()[0].text).toContain('will not be shown again');
    http.verify();
  });
});