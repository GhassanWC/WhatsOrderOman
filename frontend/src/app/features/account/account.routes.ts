import { Routes } from '@angular/router';

export const ACCOUNT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./account-shell').then((m) => m.AccountShell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./overview-page').then((m) => m.AccountOverviewPage),
      },
      {
        path: 'orders',
        loadComponent: () => import('./orders-page').then((m) => m.AccountOrdersPage),
      },
      {
        path: 'orders/:orderId',
        loadComponent: () => import('./order-detail-page').then((m) => m.AccountOrderDetailPage),
      },
      {
        path: 'messages',
        loadComponent: () => import('./messages-page').then((m) => m.AccountMessagesPage),
      },
      {
        path: 'favorites',
        loadComponent: () => import('./favorites-page').then((m) => m.AccountFavoritesPage),
      },
      {
        path: 'addresses',
        loadComponent: () => import('./addresses-page').then((m) => m.AccountAddressesPage),
      },
      {
        path: 'profile',
        loadComponent: () => import('./profile-page').then((m) => m.AccountProfilePage),
      },
      {
        path: 'offers',
        loadComponent: () => import('./account-offers-page').then((m) => m.AccountOffersPage),
      },
      {
        path: 'notifications',
        loadComponent: () => import('./notifications-page').then((m) => m.AccountNotificationsPage),
      },
      {
        path: 'payments',
        loadComponent: () => import('./payments-page').then((m) => m.AccountPaymentsPage),
      },
      {
        path: 'billing',
        loadComponent: () => import('./billing-page').then((m) => m.AccountBillingPage),
      },
    ],
  },
];
