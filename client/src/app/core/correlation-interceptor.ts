import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { tap } from 'rxjs';

import { environment } from '../../environments/environment';

export const correlationInterceptor: HttpInterceptorFn = (req, next) => {
  const correlationId = globalThis.crypto.randomUUID().replaceAll('-', '');
  return next(
    req.clone({
      setHeaders: { 'X-Correlation-ID': correlationId },
    }),
  ).pipe(
    tap({
      error: (error: unknown) => {
        if (!environment.production && error instanceof HttpErrorResponse) {
          const echoed = error.headers.get('X-Correlation-ID') ?? correlationId;
          console.error(`Request failed (${error.status}). Correlation id: ${echoed}.`);
        }
      },
    }),
  );
};
