import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/guards';
import { MarketplaceShell } from './marketplace-shell';

export const MARKETPLACE_ROUTES: Routes = [
  {
    path: '',
    component: MarketplaceShell,
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./home-page').then((m) => m.MarketplaceHomePage),
      },
      {
        path: 'stores',
        loadComponent: () => import('./stores-page').then((m) => m.StoresPage),
      },
      {
        path: 'search',
        loadComponent: () => import('./search-page').then((m) => m.SearchPage),
      },
      {
        path: 'offers',
        loadComponent: () => import('./offers-page').then((m) => m.OffersPage),
      },
      {
        path: 'account',
        canActivate: [authGuard],
        loadChildren: () => import('../account/account.routes').then((m) => m.ACCOUNT_ROUTES),
      },
    ],
  },
];
