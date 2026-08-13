import { Routes } from '@angular/router';

export const STOREFRONT_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./store-page').then((m) => m.StorePage),
  },
  {
    path: 'product/:productId',
    loadComponent: () => import('./product-page').then((m) => m.ProductPage),
  },
  {
    path: 'cart',
    loadComponent: () => import('./cart-page').then((m) => m.CartPage),
  },
  {
    path: 'checkout',
    loadComponent: () => import('./checkout-page').then((m) => m.CheckoutPage),
  },
  {
    path: 'order/:orderNumber',
    loadComponent: () => import('./order-confirmation-page').then((m) => m.OrderConfirmationPage),
  },
];
