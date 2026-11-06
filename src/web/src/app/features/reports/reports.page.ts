import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { Api } from '../../core/api';
import { DailyDeliveriesReport, DailyOrdersReport, DailyPaymentsReport } from '../../core/models';
import { InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-reports',
  imports: [FormsModule, RouterLink, InrPipe],
  templateUrl: './reports.page.html'
})
export class ReportsPage {
  private readonly api = inject(Api);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly date = signal(new Date().toISOString().slice(0, 10));
  protected readonly orders = signal<DailyOrdersReport | null>(null);
  protected readonly deliveries = signal<DailyDeliveriesReport | null>(null);
  protected readonly payments = signal<DailyPaymentsReport | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected onDateChange(event: Event): void {
    this.date.set((event.target as HTMLInputElement).value);
    this.load();
  }

  private load(): void {
    const day = this.date();
    this.error.set(null);

    forkJoin({
      orders: this.api.dailyOrders(day),
      deliveries: this.api.dailyDeliveries(day),
      payments: this.api.dailyPayments(day)
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.orders.set(result.orders);
          this.deliveries.set(result.deliveries);
          this.payments.set(result.payments);
        },
        error: () => this.error.set('Could not load the reports.')
      });
  }
}
