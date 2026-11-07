import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: '',
    title: 'Jewellery Order Book',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage)
  },
  {
    path: 'orders/new',
    title: 'New order',
    canActivate: [authGuard],
    loadComponent: () => import('./features/orders/order-entry.page').then((m) => m.OrderEntryPage)
  },
  {
    path: 'orders/:id',
    title: 'Order',
    canActivate: [authGuard],
    loadComponent: () => import('./features/orders/order-detail.page').then((m) => m.OrderDetailPage)
  },
  {
    path: 'orders',
    title: 'Orders',
    canActivate: [authGuard],
    loadComponent: () => import('./features/orders/orders.page').then((m) => m.OrdersPage)
  },
  {
    path: 'payments',
    title: 'Payments',
    canActivate: [authGuard],
    loadComponent: () => import('./features/payments/payments.page').then((m) => m.PaymentsPage)
  },
  {
    path: 'reports',
    title: 'Reports',
    canActivate: [authGuard],
    loadComponent: () => import('./features/reports/reports.page').then((m) => m.ReportsPage)
  },
  {
    path: 'customers',
    title: 'Customers',
    canActivate: [authGuard],
    loadComponent: () => import('./features/customers/customers.page').then((m) => m.CustomersPage)
  },
  { path: '**', redirectTo: '' }
];
