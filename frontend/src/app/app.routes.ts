import { Routes } from '@angular/router';
import { authGuard, guestGuard, noStoreGuard, storeGuard } from './core/guards/guards';

export const routes: Routes = [
  // Marketplace shell: homepage, discovery, search and the buyer account area.
  // Browsing requires an account — buyers and sellers alike must sign in first.
  {
    path: '',
    canActivate: [authGuard],
    loadChildren: () =>
      import('./features/marketplace/marketplace.routes').then((m) => m.MARKETPLACE_ROUTES),
  },
  // The seller marketing page moved from '/' to '/business'.
  {
    path: 'business',
    loadComponent: () => import('./features/landing/landing-page').then((m) => m.LandingPage),
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register-page').then((m) => m.RegisterPage),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password-page').then((m) => m.ForgotPasswordPage),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password-page').then((m) => m.ResetPasswordPage),
  },
  {
    path: 'onboarding',
    canActivate: [authGuard, noStoreGuard],
    loadComponent: () =>
      import('./features/onboarding/create-store-page').then((m) => m.CreateStorePage),
  },
  {
    path: 'dashboard',
    canActivate: [authGuard, storeGuard],
    loadChildren: () => import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
  },
  // Storefront lives at the root: whatsorder.om/alreem — must stay last.
  // Wrapped in the marketplace shell so customers keep one coherent navigation.
  {
    path: ':slug',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/marketplace/marketplace-shell').then((m) => m.MarketplaceShell),
    loadChildren: () =>
      import('./features/storefront/storefront.routes').then((m) => m.STOREFRONT_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
