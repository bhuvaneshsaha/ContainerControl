import { HttpErrorResponse } from '@angular/common/http';

import { problemMessage } from './problem-message';

describe('problemMessage', () => {
  it('prefers a field error, then detail, then title', () => {
    const field = new HttpErrorResponse({
      error: { title: 'Rejected', detail: 'More', errors: { host: ['Enter a host name.'] } },
    });
    expect(problemMessage(field, 'fallback')).toBe('Enter a host name.');

    const detail = new HttpErrorResponse({ error: { title: 'Rejected', detail: 'The hostname is not allowed.' } });
    expect(problemMessage(detail, 'fallback')).toBe('The hostname is not allowed.');

    const title = new HttpErrorResponse({ error: { title: 'The Engine did not answer the version ping.' } });
    expect(problemMessage(title, 'fallback')).toBe('The Engine did not answer the version ping.');
  });

  it('uses the fallback when the body is not a problem', () => {
    expect(problemMessage(new HttpErrorResponse({ error: 'nope' }), 'fallback')).toBe('fallback');
    expect(problemMessage(new Error('x'), 'fallback')).toBe('fallback');
  });
});
