import { appendLogLine } from './log-tail';

describe('appendLogLine', () => {
  it('keeps the newest two hundred lines', () => {
    let text = '';
    for (let index = 0; index < 205; index += 1) {
      text = appendLogLine(text, `line ${index}`);
    }

    const rows = text.split('\n');
    expect(rows).toHaveLength(200);
    expect(rows[0]).toBe('line 5');
    expect(rows[199]).toBe('line 204');
  });
});