import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth.service';
import { Role } from '../models/auth';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() || inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const guestGuard: CanActivateFn = () => {
  return !inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/']);
};

/** Usage: `canActivate: [roleGuard('Admin', 'Teacher')]` */
export const roleGuard =
  (...roles: Role[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const role = auth.role();
    if (role && roles.includes(role)) return true;
    return inject(Router).createUrlTree([auth.isAuthenticated() ? '/forbidden' : '/login']);
  };
