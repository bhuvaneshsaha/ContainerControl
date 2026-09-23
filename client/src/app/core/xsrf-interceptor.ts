import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';

import { XsrfToken } from './xsrf-token';

export const xsrfInterceptor: HttpInterceptorFn = (req, next) => {
  const xsrf = inject(XsrfToken);
  const token = xsrf.current();
  const method = req.method.toUpperCase();
  const outgoing =
    token && method !== 'GET' && method !== 'HEAD' && method !== 'OPTIONS'
      ? req.clone({ setHeaders: { 'X-XSRF-TOKEN': token } })
      : req;

  return next(outgoing).pipe(
    tap((event) => {
      if (event instanceof HttpResponse) {
        xsrf.remember(event.headers.get('X-XSRF-TOKEN'));
      }
    }),
  );
};
