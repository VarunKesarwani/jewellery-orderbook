import { Component, inject, signal } from '@angular/core';

import { Api } from '../../core/api';
import { Order } from '../../core/models';
import { GramsPipe, InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-orders',
  imports: [InrPipe, GramsPipe],
  template: `
    <section class="card">
      <h1>Orders</h1>

      <div class="toolbar">
        <label for="status">Status</label>
        <select id="status" [value]="status()" (change)="onStatusChange($event)">
          <option value="">All</option>
          <option value="Pending">Pending</option>
          <option value="InProgress">In progress</option>
          <option value="Ready">Ready</option>
          <option value="Delivered">Delivered</option>
          <option value="Cancelled">Cancelled</option>
        </select>
        <span class="muted">{{ orders().length }} shown</span>
      </div>

      @if (loading()) {
        <p class="muted">Loading…</p>
      } @else if (orders().length === 0) {
        <p class="muted">No orders yet.</p>
      } @else {
        <ul class="list">
          @for (order of orders(); track order.id) {
            <li>
              <div class="list__main">
                <strong>#{{ order.number }} · {{ order.customerName }}</strong>
                <span class="muted">{{ order.orderDate }} · {{ order.status }}</span>
              </div>
              <div class="list__side">
                <span class="pill" [class.pill--accent]="order.paymentState === 'Paid'">
                  {{ order.paymentState }}
                </span>
                <strong>{{ order.finalAmountPaise | inr }}</strong>
              </div>
              <div class="list__lines">
                @for (line of order.lines; track line.id) {
                  <span class="muted">
                    {{ line.itemName }} · {{ line.grossWeightMg | grams }} · {{ line.metalPurity }} ·
                    tax {{ line.taxPaise | inr }}
                  </span>
                }
              </div>
            </li>
          }
        </ul>
      }
    </section>
  `
})
export class OrdersPage {
  private readonly api = inject(Api);

  protected readonly orders = signal<Order[]>([]);
  protected readonly status = signal('');
  protected readonly loading = signal(true);

  constructor() {
    this.load();
  }

  protected onStatusChange(event: Event): void {
    this.status.set((event.target as HTMLSelectElement).value);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.listOrders(this.status() || undefined).subscribe({
      next: (orders) => {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
