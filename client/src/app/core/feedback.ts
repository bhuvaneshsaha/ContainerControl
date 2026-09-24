import { Injectable, signal } from '@angular/core';

export interface FeedbackNotice {
  id: number;
  kind: 'status' | 'alert';
  text: string;
}

@Injectable({
  providedIn: 'root',
})
export class FeedbackService {
  private readonly notices = signal<readonly FeedbackNotice[]>([]);
  private nextId = 1;
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();

  readonly items = this.notices.asReadonly();

  success(text: string): void {
    this.push('status', text, 6000);
  }

  /** Polite notice, including a forbidden deep-link. Stays long enough to read after navigation. */
  status(text: string, dismissMs = 12000): void {
    this.push('status', text, dismissMs);
  }

  error(text: string): void {
    this.push('alert', text);
  }

  dismiss(id: number): void {
    const timer = this.timers.get(id);
    if (timer !== undefined) {
      clearTimeout(timer);
      this.timers.delete(id);
    }
    this.notices.update((items) => items.filter((item) => item.id !== id));
  }

  clear(): void {
    for (const timer of this.timers.values()) {
      clearTimeout(timer);
    }
    this.timers.clear();
    this.notices.set([]);
  }

  private push(kind: FeedbackNotice['kind'], text: string, dismissMs?: number): void {
    const id = this.nextId++;
    this.notices.update((items) => [...items, { id, kind, text }]);
    if (dismissMs !== undefined) {
      this.timers.set(
        id,
        setTimeout(() => this.dismiss(id), dismissMs),
      );
    }
  }
}
