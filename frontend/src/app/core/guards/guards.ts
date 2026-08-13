import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return true;
  // Sellers continue to their dashboard flow; buyers land on the marketplace.
  if (auth.isSeller()) {
    return auth.user()?.hasStore
      ? router.createUrlTree(['/dashboard'])
      : router.createUrlTree(['/onboarding']);
  }
  return router.createUrlTree(['/']);
};

/** Dashboard requires a store; owners without one are sent to onboarding. */
export const storeGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.user()?.hasStore ? true : router.createUrlTree(['/onboarding']);
};

/** Onboarding is only for users who don't have a store yet. */
export const noStoreGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.user()?.hasStore ? router.createUrlTree(['/dashboard']) : true;
};
