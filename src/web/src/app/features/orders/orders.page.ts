import { Component, computed, inject, signal } from '@angular/core';

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
          <option value="Draft">Draft</option>
          <option value="Pending">Pending</option>
          <option value="InProgress">In progress</option>
          <option value="Ready">Ready</option>
          <option value="Delivered">Delivered</option>
          <option value="Cancelled">Cancelled</option>
        </select>
        <span class="muted">{{ filtered().length }} shown</span>
      </div>

      @if (loading()) {
        <p class="muted">Loading…</p>
      } @else if (filtered().length === 0) {
        <p class="muted">No orders yet. Create one from the API or the app once the entry form lands.</p>
      } @else {
        <ul class="list">
          @for (order of filtered(); track order.id) {
            <li>
              <div class="list__main">
                <strong>{{ order.number || 'Draft' }}</strong>
                <span class="muted">{{ order.orderDate }} · {{ order.orderType }}</span>
              </div>
              <div class="list__side">
                <span class="pill" [class.pill--accent]="order.status === 'Ready'">{{ order.status }}</span>
                <strong>{{ order.totalPaise | inr }}</strong>
              </div>
              <div class="list__lines">
                @for (line of order.lines; track line.id) {
                  <span class="muted">
                    {{ line.itemName }} · {{ line.grossWeightMg | grams }} · {{ line.metalPurity }}
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

  protected readonly filtered = computed(() => this.orders());

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
