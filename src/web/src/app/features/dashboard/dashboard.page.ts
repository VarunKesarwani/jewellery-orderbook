import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Api } from '../../core/api';
import { CurrentUser, Order } from '../../core/models';
import { InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, InrPipe],
  template: `
    <section class="card">
      <h1>Jewellery Order Book</h1>
      <p class="muted">
        Orders, delivery and payments for a single-tenant jewellery business. This build is the
        walking skeleton: the plumbing works end to end, the pricing engine is still a placeholder.
      </p>

      @if (user(); as me) {
        <p class="pill-row">
          <span class="pill">signed in as {{ me.name }}</span>
          @for (role of me.roles; track role) {
            <span class="pill pill--accent">{{ role }}</span>
          }
        </p>
      }

      <dl class="stats">
        <div>
          <dt>Orders</dt>
          <dd>{{ orders().length }}</dd>
        </div>
        <div>
          <dt>Open value</dt>
          <dd>{{ openValue() | inr }}</dd>
        </div>
      </dl>

      <p class="actions">
        <a class="button" routerLink="/orders">View orders</a>
        <a class="button button--ghost" routerLink="/customers">View customers</a>
      </p>
    </section>

    @if (error()) {
      <section class="card card--error">
        <strong>Could not reach the API.</strong>
        <p class="muted">{{ error() }}</p>
      </section>
    }
  `
})
export class DashboardPage {
  private readonly api = inject(Api);

  protected readonly user = signal<CurrentUser | null>(null);
  protected readonly orders = signal<Order[]>([]);
  protected readonly error = signal<string | null>(null);

  protected openValue = () => this.orders().reduce((sum, o) => sum + o.totalPaise, 0);

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
