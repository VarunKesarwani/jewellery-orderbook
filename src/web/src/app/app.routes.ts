import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Jewellery Order Book',
    loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage)
  },
  {
    path: 'orders',
    title: 'Orders',
    loadComponent: () => import('./features/orders/orders.page').then((m) => m.OrdersPage)
  },
  {
    path: 'customers',
    title: 'Customers',
    loadComponent: () => import('./features/customers/customers.page').then((m) => m.CustomersPage)
  },
  { path: '**', redirectTo: '' }
];
