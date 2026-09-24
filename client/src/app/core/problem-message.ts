import { HttpErrorResponse } from '@angular/common/http';

export function problemMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse) || !error.error || typeof error.error !== 'object') {
    return fallback;
  }

  const body = error.error as { title?: string; detail?: string; errors?: Record<string, string[]> };
  const field = body.errors && Object.values(body.errors).flat().find((item) => item.length > 0);
  return field || body.detail || body.title || fallback;
}
