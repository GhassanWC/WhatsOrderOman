import { Routes } from '@angular/router';

export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./dashboard-shell').then((m) => m.DashboardShell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./home-page').then((m) => m.DashboardHomePage),
      },
      {
        path: 'products',
        loadComponent: () => import('../products/products-page').then((m) => m.ProductsPage),
      },
      {
        path: 'products/new',
        loadComponent: () => import('../products/product-editor-page').then((m) => m.ProductEditorPage),
      },
      {
        path: 'products/:id/edit',
        loadComponent: () => import('../products/product-editor-page').then((m) => m.ProductEditorPage),
      },
      {
        path: 'orders',
        loadComponent: () => import('../orders/orders-page').then((m) => m.OrdersPage),
      },
      {
        path: 'offers',
        loadComponent: () => import('./offers-page').then((m) => m.SellerOffersPage),
      },
      {
        path: 'reviews',
        loadComponent: () => import('./reviews-page').then((m) => m.SellerReviewsPage),
      },
      {
        path: 'settings',
        loadComponent: () => import('../settings/settings-page').then((m) => m.SettingsPage),
      },
    ],
  },
];
