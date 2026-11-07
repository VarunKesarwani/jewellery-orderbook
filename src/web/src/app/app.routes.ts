import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Jewellery Order Book',
    loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage)
  },
  {
    path: 'orders/new',
    title: 'New order',
    loadComponent: () => import('./features/orders/order-entry.page').then((m) => m.OrderEntryPage)
  },
  {
    path: 'orders/:id',
    title: 'Order',
    loadComponent: () => import('./features/orders/order-detail.page').then((m) => m.OrderDetailPage)
  },
  {
    path: 'orders',
    title: 'Orders',
    loadComponent: () => import('./features/orders/orders.page').then((m) => m.OrdersPage)
  },
  {
    path: 'payments',
    title: 'Payments',
    loadComponent: () => import('./features/payments/payments.page').then((m) => m.PaymentsPage)
  },
  {
    path: 'reports',
    title: 'Reports',
    loadComponent: () => import('./features/reports/reports.page').then((m) => m.ReportsPage)
  },
  {
    path: 'customers',
    title: 'Customers',
    loadComponent: () => import('./features/customers/customers.page').then((m) => m.CustomersPage)
  },
  { path: '**', redirectTo: '' }
];
