import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Attaches the bearer token to API calls and transparently retries a request once
 * after a silent refresh when the access token has expired (401).
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const request = withToken(req, auth.accessToken());

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthEndpoint = req.url.includes('/api/auth/');
      if (error.status === 401 && auth.isAuthenticated() && !isAuthEndpoint) {
        return auth.refresh().pipe(
          switchMap(() => next(withToken(req, auth.accessToken()))),
          catchError((refreshError) => {
            auth.clear();
            void router.navigate(['/login']);
            return throwError(() => refreshError);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  if (!token || !req.url.startsWith('/api') || req.url.includes('/api/auth/refresh')) {
    return req;
  }
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
