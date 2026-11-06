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
  template: `
    <section class="card">
      <div class="toolbar">
        <h1>Daily reports</h1>
        <span class="spacer"></span>
        <label class="inline">
          Date
          <input type="date" [value]="date()" (change)="onDateChange($event)" />
        </label>
      </div>

      <p class="muted">
        Three views of one day, each grouped by its own date — orders by when they were raised,
        deliveries by when they went out, payments by when the money arrived. They will not tie
        out against each other, and are not meant to.
      </p>

      @if (error()) {
        <p class="card--error">{{ error() }}</p>
      }

      <dl class="stats">
        <div>
          <dt>Orders</dt>
          <dd>{{ orders()?.orderCount ?? 0 }}</dd>
        </div>
        <div>
          <dt>Order value</dt>
          <dd>{{ (orders()?.valuePaise ?? 0) | inr }}</dd>
        </div>
        <div>
          <dt>Delivered</dt>
          <dd>{{ deliveries()?.pieces ?? 0 }}</dd>
        </div>
        <div>
          <dt>Collected</dt>
          <dd>{{ (payments()?.netPaise ?? 0) | inr }}</dd>
        </div>
      </dl>

      <!-- orders -->
      <h2>Orders raised</h2>
      @if (!orders() || orders()!.rows.length === 0) {
        <p class="muted">No orders on {{ date() }}.</p>
      } @else {
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Order</th><th>Customer</th><th>Pieces</th><th>Subtotal</th><th>Tax</th><th>Total</th></tr>
            </thead>
            <tbody>
              @for (row of orders()!.rows; track row.number) {
                <tr>
                  <td><a [routerLink]="['/orders', row.id]" class="plain-link">#{{ row.number }}</a></td>
                  <td>{{ row.customer || '—' }}</td>
                  <td>{{ row.pieces }}</td>
                  <td>{{ row.subtotalPaise | inr }}</td>
                  <td>{{ row.taxPaise | inr }}</td>
                  <td><strong>{{ row.finalAmountPaise | inr }}</strong></td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr>
                <td colspan="3">{{ orders()!.orderCount }} orders</td>
                <td></td>
                <td>{{ orders()!.taxPaise | inr }}</td>
                <td><strong>{{ orders()!.valuePaise | inr }}</strong></td>
              </tr>
            </tfoot>
          </table>
        </div>
      }

      <!-- deliveries -->
      <h2>Deliveries made</h2>
      @if (!deliveries() || deliveries()!.rows.length === 0) {
        <p class="muted">Nothing delivered on {{ date() }}.</p>
      } @else {
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Delivery</th><th>Order</th><th>Customer</th><th>Pieces</th><th>Received by</th></tr>
            </thead>
            <tbody>
              @for (row of deliveries()!.rows; track row.number) {
                <tr>
                  <td>#{{ row.number }}</td>
                  <td>#{{ row.orderNumber ?? '—' }}</td>
                  <td>{{ row.customer || '—' }}</td>
                  <td>{{ row.pieces }}</td>
                  <td>{{ row.receivedBy || '—' }}</td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr>
                <td colspan="3">{{ deliveries()!.deliveryCount }} deliveries</td>
                <td><strong>{{ deliveries()!.pieces }}</strong></td>
                <td></td>
              </tr>
            </tfoot>
          </table>
        </div>
      }

      <!-- payments -->
      <h2>Money received</h2>
      @if (!payments() || payments()!.rows.length === 0) {
        <p class="muted">Nothing received on {{ date() }}.</p>
      } @else {
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Payment</th><th>Order</th><th>Method</th><th>Reference</th><th>Amount</th></tr>
            </thead>
            <tbody>
              @for (row of payments()!.rows; track row.number) {
                <tr>
                  <td>#{{ row.number }}</td>
                  <td>#{{ row.orderNumber ?? '—' }}</td>
                  <td>{{ row.method || '—' }}</td>
                  <td>{{ row.reference || '—' }}</td>
                  <td>
                    @if (row.isRefund) {
                      <span class="pill">refund</span>
                    }
                    <strong>{{ (row.isRefund ? -row.amountPaise : row.amountPaise) | inr }}</strong>
                  </td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr>
                <td colspan="4">
                  Collected {{ payments()!.collectedPaise | inr }}
                  @if (payments()!.refundedPaise > 0) {
                    · refunded {{ payments()!.refundedPaise | inr }}
                  }
                </td>
                <td><strong>{{ payments()!.netPaise | inr }}</strong></td>
              </tr>
            </tfoot>
          </table>
        </div>
      }
    </section>
  `
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
