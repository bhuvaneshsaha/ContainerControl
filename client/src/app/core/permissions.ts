import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PermissionService {
  private readonly codes = signal<readonly string[]>([]);

  setPermissions(codes: readonly string[]): void {
    this.codes.set([...codes]);
  }

  hasPermission(code: string): boolean {
    return this.codes().includes(code);
  }

  hasAny(codes: readonly string[]): boolean {
    return codes.some((code) => this.codes().includes(code));
  }

  list(): readonly string[] {
    return this.codes();
  }
}
