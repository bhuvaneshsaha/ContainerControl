import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class XsrfToken {
  private value: string | null = null;

  remember(token: string | null): void {
    if (token) {
      this.value = token;
    }
  }

  current(): string | null {
    return this.value;
  }
}
