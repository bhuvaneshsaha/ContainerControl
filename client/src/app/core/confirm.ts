import { Injectable, signal } from '@angular/core';

export interface ConfirmRequest {
  title: string;
  body: string;
  confirmLabel: string;
  cancelLabel: string;
  irreversible: boolean;
  resolve: (confirmed: boolean) => void;
}

@Injectable({
  providedIn: 'root',
})
export class ConfirmService {
  readonly request = signal<ConfirmRequest | null>(null);

  ask(options: {
    title: string;
    body: string;
    confirmLabel: string;
    cancelLabel?: string;
    irreversible?: boolean;
  }): Promise<boolean> {
    const pending = this.request();
    if (pending) {
      return Promise.resolve(false);
    }

    return new Promise((resolve) => {
      this.request.set({
        title: options.title,
        body: options.body,
        confirmLabel: options.confirmLabel,
        cancelLabel: options.cancelLabel ?? 'Cancel',
        irreversible: options.irreversible ?? false,
        resolve,
      });
    });
  }

  answer(confirmed: boolean): void {
    const pending = this.request();
    if (!pending) {
      return;
    }

    this.request.set(null);
    pending.resolve(confirmed);
  }
}
