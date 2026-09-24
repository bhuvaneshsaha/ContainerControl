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

  clear(): void {
    this.value = null;
  }

  current(): string | null {
    return this.value;
  }
}
