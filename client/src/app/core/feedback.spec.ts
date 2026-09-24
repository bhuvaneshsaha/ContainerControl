import { TestBed } from '@angular/core/testing';

import { FeedbackService } from './feedback';

describe('FeedbackService', () => {
  let feedback: FeedbackService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    feedback = TestBed.inject(FeedbackService);
  });

  afterEach(() => {
    feedback.clear();
  });

  it('keeps an error as an alert until it is dismissed', () => {
    feedback.error('The application could not be saved.');
    const notice = feedback.items()[0];
    expect(notice.kind).toBe('alert');
    feedback.dismiss(notice.id);
    expect(feedback.items()).toEqual([]);
  });

  it('records a success as a status notice', () => {
    feedback.success('welcome was saved.');
    expect(feedback.items()[0].kind).toBe('status');
    expect(feedback.items()[0].text).toBe('welcome was saved.');
  });
});
