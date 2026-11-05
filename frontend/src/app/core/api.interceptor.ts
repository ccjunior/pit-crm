import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const safeMethods = new Set(['GET', 'HEAD', 'OPTIONS']);

export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const apiRequest = request.url.startsWith('/api/');
  const withCredentials = apiRequest ? request.clone({ withCredentials: true }) : request;
  const send = (securedRequest: typeof request) => next(securedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !securedRequest.url.endsWith('/api/auth/login')) {
        auth.expireSession();
        void router.navigate(['/entrar']);
      }
      return throwError(() => error);
    }),
  );

  if (!apiRequest || safeMethods.has(request.method)) return send(withCredentials);

  return auth.getCsrfToken().pipe(
    switchMap((token) => send(withCredentials.clone({ setHeaders: { 'X-CSRF-TOKEN': token } }))),
  );
};
