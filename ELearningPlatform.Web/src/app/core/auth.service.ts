import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { API_URL } from './api.config';
import { AuthDto, LoginRequest, Role, TwoFactorMethod } from './models/auth';

const ACCOUNT = `${API_URL}/Account`;
const withCreds = { withCredentials: true };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // The access token lives in memory only; the refresh cookie restores the session on reload.
  private readonly session = signal<AuthDto | null>(null);
  private refreshing$: Observable<AuthDto | null> | null = null;

  readonly token = computed(() => this.session()?.accessToken ?? null);
  readonly role = computed<Role | null>(() => this.session()?.role ?? null);
  readonly userId = computed(() => this.session()?.userId ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  login(body: LoginRequest) {
    return this.http.post<AuthDto>(`${ACCOUNT}/login`, body, withCreds).pipe(tap((s) => this.session.set(s)));
  }

  googleLogin(idToken: string) {
    return this.http
      .post<AuthDto>(`${ACCOUNT}/google-login`, { idToken, provider: 'Google' }, withCreds)
      .pipe(tap((s) => this.session.set(s)));
  }

  sendTwoFactorCode(email: string, method: TwoFactorMethod) {
    return this.http.post(`${ACCOUNT}/2fa/send-code`, { email, method });
  }

  verifyTwoFactor(email: string, method: TwoFactorMethod, code: string) {
    return this.http
      .post<AuthDto>(`${ACCOUNT}/2fa-verify`, { email, method, code }, withCreds)
      .pipe(tap((s) => this.session.set(s)));
  }

  /** Rotates the refresh cookie. Concurrent callers share one request, since rotation invalidates the old token. */
  refresh(): Observable<AuthDto | null> {
    this.refreshing$ ??= this.http.post<AuthDto>(`${ACCOUNT}/refresh-token`, {}, withCreds).pipe(
      tap((s) => this.session.set(s)),
      catchError(() => {
        this.session.set(null);
        return of(null);
      }),
      finalize(() => (this.refreshing$ = null)),
      shareReplay(1),
    );
    return this.refreshing$;
  }

  /** Called at startup: restores the session from the refresh cookie, if there is one. */
  restore(): Observable<boolean> {
    return this.refresh().pipe(map((s) => s !== null));
  }

  logout() {
    return this.http.post(`${ACCOUNT}/logout`, {}, withCreds).pipe(
      catchError(() => of(null)),
      finalize(() => {
        this.session.set(null);
        this.router.navigateByUrl('/login');
      }),
    );
  }

  /** Drops the local session without calling the API (e.g. after a password change, which clears the cookie). */
  clear() {
    this.session.set(null);
  }
}
