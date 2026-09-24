import { HttpBackend, HttpClient, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap, tap } from 'rxjs';

import { environment } from '../../environments/environment';
import { XsrfToken } from './xsrf-token';

const safeMethods = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

export const xsrfInterceptor: HttpInterceptorFn = (req, next) => {
  const xsrf = inject(XsrfToken);
  const backend = inject(HttpBackend);
  const method = req.method.toUpperCase();

  const send = (token: string | null) => {
    const outgoing =
      token && !safeMethods.has(method)
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

  if (safeMethods.has(method) || req.url.endsWith('/auth/csrf') || xsrf.current()) {
    return send(xsrf.current());
  }

  const http = new HttpClient(backend);
  return from(
    http.get(`${environment.apiUrl}/auth/csrf`, {
      observe: 'response',
      withCredentials: true,
    }),
  ).pipe(
    tap((response) => xsrf.remember(response.headers.get('X-XSRF-TOKEN'))),
    switchMap((response) => send(response.headers.get('X-XSRF-TOKEN'))),
  );
};
