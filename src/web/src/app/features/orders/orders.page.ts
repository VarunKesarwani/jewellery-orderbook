import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { Api } from '../../core/api';
import { Order } from '../../core/models';
import { GramsPipe, InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-orders',
  imports: [InrPipe, GramsPipe, RouterLink],
  templateUrl: './orders.page.html'
})
export class OrdersPage {
  private readonly api = inject(Api);

  protected readonly orders = signal<Order[]>([]);
  protected readonly status = signal('');
  protected readonly created = signal<string | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    this.created.set(inject(ActivatedRoute).snapshot.queryParamMap.get('created'));
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
