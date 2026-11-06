import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Api } from '../../core/api';
import { CurrentUser, Order } from '../../core/models';
import { InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, InrPipe],
  templateUrl: './dashboard.page.html'
})
export class DashboardPage {
  private readonly api = inject(Api);

  protected readonly user = signal<CurrentUser | null>(null);
  protected readonly orders = signal<Order[]>([]);
  protected readonly error = signal<string | null>(null);

  protected orderValue = () => this.orders().reduce((sum, o) => sum + o.finalAmountPaise, 0);
  protected collected = () => this.orders().reduce((sum, o) => sum + o.paidPaise, 0);

  constructor() {
    this.api.me().subscribe({
      next: (me) => this.user.set(me),
      error: (err) => this.error.set(err.message ?? 'Unknown error')
    });

    this.api.listOrders().subscribe({
      next: (orders) => this.orders.set(orders),
      error: (err) => this.error.set(err.message ?? 'Unknown error')
    });
  }
}
