import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_URL } from './api.config';
import { AuthService } from './auth.service';

const isAnonymousAuthCall = (url: string) =>
  /\/Account\/(login|google-login|refresh-token|register-|forgot-password|reset-password|2fa-verify|2fa\/send-code|confirm-email|resend-confirm-email)/.test(url);

function withBearer(req: HttpRequest<unknown>, token: string | null) {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_URL)) return next(req);

  const auth = inject(AuthService);
  const router = inject(Router);

  // Cookie must be sent to /api/Account/*; everything else only needs the Bearer token.
  const base = req.url.startsWith(`${API_URL}/Account`) ? req.clone({ withCredentials: true }) : req;
  const authorized = withBearer(base, auth.token());

  return next(authorized).pipe(
    catchError((err: HttpErrorResponse) => {
      // A 401 carrying a problem `code` is a business error (e.g. 2FA required), not an expired token.
      const expired = err.status === 401 && !err.error?.code && !isAnonymousAuthCall(req.url);
      if (!expired) return throwError(() => err);

      return auth.refresh().pipe(
        switchMap((session) => {
          if (!session) {
            router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
            return throwError(() => err);
          }
          return next(withBearer(base, session.accessToken));
        }),
      );
    }),
  );
};
