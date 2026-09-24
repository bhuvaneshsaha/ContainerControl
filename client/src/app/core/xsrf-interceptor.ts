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
      token && !safeMethods.has(method) && !req.headers.has('X-XSRF-TOKEN')
        ? req.clone({ setHeaders: { 'X-XSRF-TOKEN': token } })
        : req;
    return next(outgoing).pipe(
      tap((event) => {
        if (event instanceof HttpResponse) {
          xsrf.remember(readToken(event));
        }
      }),
    );
  };

  if (safeMethods.has(method) || req.url.endsWith('/auth/csrf') || req.headers.has('X-XSRF-TOKEN')) {
    return send(null);
  }

  const http = new HttpClient(backend);
  return from(
    http.get(`${environment.apiUrl}/auth/csrf`, {
      observe: 'response',
      withCredentials: true,
    }),
  ).pipe(
    tap((response) => xsrf.remember(readToken(response))),
    switchMap((response) => send(readToken(response))),
  );
};

function readToken(response: HttpResponse<unknown>): string | null {
  const header = response.headers.get('X-XSRF-TOKEN');
  if (header) {
    return header;
  }

  const body = response.body;
  if (body && typeof body === 'object' && 'token' in body && typeof body.token === 'string') {
    return body.token;
  }

  return null;
}
