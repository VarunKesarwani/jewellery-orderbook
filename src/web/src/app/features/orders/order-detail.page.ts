import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { Api } from '../../core/api';
import { Delivery, Order, Payment } from '../../core/models';
import { GramsPipe, InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-order-detail',
  imports: [ReactiveFormsModule, RouterLink, InrPipe, GramsPipe],
  template: `
    <section class="card">
      <div class="toolbar">
        <h1>Order #{{ order()?.number ?? '…' }}</h1>
        <span class="spacer"></span>
        <a class="button button--ghost" routerLink="/orders">Back</a>
      </div>

      @if (order(); as o) {
        <p class="pill-row">
          <span class="pill" [class.pill--accent]="o.status === 'Delivered'">{{ o.status }}</span>
          <span class="pill" [class.pill--accent]="o.paymentState === 'Paid'">{{ o.paymentState }}</span>
          <span class="pill">{{ o.customerName }}</span>
          <span class="pill">{{ o.orderDate }}</span>
        </p>

        <dl class="order-totals">
          <div><dt>Subtotal</dt><dd>{{ o.subtotalPaise | inr }}</dd></div>
          <div><dt>Tax</dt><dd>{{ o.taxPaise | inr }}</dd></div>
          <div><dt>Paid</dt><dd>{{ o.paidPaise | inr }}</dd></div>
          <div class="strong"><dt>Total</dt><dd>{{ o.finalAmountPaise | inr }}</dd></div>
        </dl>

        <h2>Items</h2>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Item</th><th>Weight</th><th>Ordered</th><th>Delivered</th><th>Remaining</th><th>Total</th></tr>
            </thead>
            <tbody>
              @for (line of o.lines; track line.id) {
                <tr>
                  <td>{{ line.itemName }}<br /><small class="muted">{{ line.metalPurity }} · {{ line.metalType }}</small></td>
                  <td>{{ line.grossWeightMg | grams }}</td>
                  <td>{{ line.quantity }}</td>
                  <td>{{ line.deliveredPieces }}</td>
                  <td>{{ line.quantity - line.deliveredPieces }}</td>
                  <td>{{ line.lineTotalPaise | inr }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        @if (message()) {
          <p class="banner">{{ message() }}</p>
        }
        @if (error()) {
          <p class="card--error">{{ error() }}</p>
        }

        <!-- delivery -->
        @if (o.status !== 'Cancelled' && o.status !== 'Delivered') {
          <h2>Record a delivery</h2>
          <form [formGroup]="deliveryForm" (ngSubmit)="recordDelivery()">
            <div class="grid-3">
              <label>Delivered on <input type="date" formControlName="deliveredOn" /></label>
              <label>Received by <input formControlName="receivedBy" placeholder="Name at the counter" /></label>
              <label>Notes <input formControlName="notes" /></label>
            </div>

            <div class="table-wrap">
              <table>
                <thead><tr><th>Item</th><th>Remaining</th><th>Delivering now</th></tr></thead>
                <tbody>
                  @for (line of o.lines; track line.id) {
                    <tr>
                      <td>{{ line.itemName }}</td>
                      <td>{{ line.quantity - line.deliveredPieces }}</td>
                      <td>
                        <input
                          type="number"
                          min="0"
                          [max]="line.quantity - line.deliveredPieces"
                          [value]="quantityFor(line.id)"
                          [disabled]="line.quantity - line.deliveredPieces === 0"
                          (input)="setQuantity(line.id, $event)" />
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <p class="actions">
              <button class="button" type="submit" [disabled]="busy()">Record delivery</button>
            </p>
          </form>
        }

        <!-- payment -->
        @if (o.status !== 'Cancelled') {
          <h2>Record a payment</h2>
          <form [formGroup]="paymentForm" (ngSubmit)="recordPayment()">
            <div class="grid-4">
              <label>Amount (₹) <input type="number" step="0.01" min="0" formControlName="amountRupees" /></label>
              <label>Received on <input type="date" formControlName="paidOn" /></label>
              <label>
                Method
                <select formControlName="method">
                  <option value="UPI">UPI</option>
                  <option value="Cash">Cash</option>
                  <option value="Card">Card</option>
                  <option value="Bank">Bank transfer</option>
                </select>
              </label>
              <label>Reference <input formControlName="reference" /></label>
            </div>
            <p class="actions">
              <button class="button" type="submit" [disabled]="busy()">Record payment</button>
              <span class="muted">Balance {{ balance() | inr }}</span>
            </p>
          </form>
        }

        <!-- status and cancellation -->
        @if (o.status !== 'Cancelled' && o.status !== 'Delivered') {
          <h2>Status</h2>
          <p class="actions">
            <button type="button" class="button button--ghost" (click)="setStatus('InProgress')">In progress</button>
            <button type="button" class="button button--ghost" (click)="setStatus('Ready')">Ready</button>
          </p>

          @if (o.deliveredPieces === 0) {
            <form [formGroup]="cancelForm" (ngSubmit)="cancel()">
              <div class="grid-3">
                <label>Cancellation reason <input formControlName="reason" placeholder="Customer changed their mind" /></label>
              </div>
              <p class="actions">
                <button class="button button--ghost" type="submit" [disabled]="busy() || !cancelForm.value.reason">
                  Cancel order and refund
                </button>
              </p>
            </form>
          } @else {
            <p class="muted">This order cannot be cancelled — something has already been delivered.</p>
          }
        }

        @if (o.cancellationReason) {
          <p class="muted">Cancelled: {{ o.cancellationReason }}</p>
        }

        <h2>Deliveries</h2>
        @if (deliveries().length === 0) {
          <p class="muted">Nothing delivered yet.</p>
        } @else {
          <ul class="list">
            @for (d of deliveries(); track d.id) {
              <li>
                <div class="list__main">
                  <strong>#{{ d.number }}</strong>
                  <span class="muted">{{ d.deliveredOn }} · {{ d.totalPieces }} pieces</span>
                </div>
                <div class="list__side"><span class="pill">{{ d.receivedBy || 'no name' }}</span></div>
              </li>
            }
          </ul>
        }

        <h2>Payments</h2>
        @if (payments().length === 0) {
          <p class="muted">Nothing received yet.</p>
        } @else {
          <ul class="list">
            @for (p of payments(); track p.id) {
              <li>
                <div class="list__main">
                  <strong>#{{ p.number }}</strong>
                  <span class="muted">{{ p.paidOn }} · {{ p.method }}</span>
                </div>
                <div class="list__side">
                  @if (p.isRefund) {
                    <span class="pill">refund</span>
                  }
                  <strong>{{ (p.isRefund ? -p.amountPaise : p.amountPaise) | inr }}</strong>
                </div>
              </li>
            }
          </ul>
        }
      } @else if (error()) {
        <p class="card--error">{{ error() }}</p>
      } @else {
        <p class="muted">Loading…</p>
      }
    </section>
  `
})
export class OrderDetailPage {
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  private readonly orderId = this.route.snapshot.paramMap.get('id') ?? '';
  private readonly today = new Date().toISOString().slice(0, 10);

  protected readonly order = signal<Order | null>(null);
  protected readonly deliveries = signal<Delivery[]>([]);
  protected readonly payments = signal<Payment[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);
  private readonly quantities = signal<Record<string, number>>({});

  protected readonly deliveryForm = this.fb.group({
    deliveredOn: [this.today, Validators.required],
    receivedBy: [''],
    notes: ['']
  });

  protected readonly paymentForm = this.fb.group({
    amountRupees: [0, [Validators.min(0)]],
    paidOn: [this.today, Validators.required],
    method: ['UPI'],
    reference: ['']
  });

  protected readonly cancelForm = this.fb.group({ reason: [''] });

  protected readonly balance = () => {
    const o = this.order();
    return o ? Math.max(0, o.finalAmountPaise - o.paidPaise) : 0;
  };

  constructor() {
    this.load();
  }

  protected quantityFor(lineId: string): number {
    return this.quantities()[lineId] ?? 0;
  }

  protected setQuantity(lineId: string, event: Event): void {
    const value = Number((event.target as HTMLInputElement).value) || 0;
    this.quantities.update((q) => ({ ...q, [lineId]: value }));
  }

  protected recordDelivery(): void {
    const order = this.order();
    if (!order) {
      return;
    }

    const lines = order.lines
      .map((line) => ({ orderLineId: line.id, quantity: this.quantityFor(line.id) }))
      .filter((line) => line.quantity > 0);

    if (lines.length === 0) {
      this.error.set('Enter how many pieces are being delivered.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.api
      .createDelivery({
        orderId: order.id,
        deliveredOn: this.deliveryForm.value.deliveredOn ?? this.today,
        receivedBy: this.deliveryForm.value.receivedBy || null,
        notes: this.deliveryForm.value.notes || null,
        lines
      })
      .subscribe({
        next: (delivery) => {
          this.message.set(`Delivery #${delivery.number} recorded.`);
          this.deliveryForm.patchValue({ receivedBy: '', notes: '' });
          this.busy.set(false);
          this.load();
        },
        error: (err) => this.fail(err, 'Could not record the delivery.')
      });
  }

  protected recordPayment(): void {
    const order = this.order();
    const amount = Math.round((Number(this.paymentForm.value.amountRupees) || 0) * 100);

    if (!order || amount <= 0) {
      this.error.set('Enter an amount greater than zero.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.api
      .createPayment({
        orderId: order.id,
        amountPaise: amount,
        paidOn: this.paymentForm.value.paidOn ?? this.today,
        method: this.paymentForm.value.method || null,
        reference: this.paymentForm.value.reference || null
      })
      .subscribe({
        next: (payment) => {
          this.message.set(`Payment #${payment.number} recorded.`);
          this.paymentForm.patchValue({ amountRupees: 0, reference: '' });
          this.busy.set(false);
          this.load();
        },
        error: (err) => this.fail(err, 'Could not record the payment.')
      });
  }

  protected setStatus(status: string): void {
    this.busy.set(true);
    this.api.changeOrderStatus(this.orderId, status).subscribe({
      next: () => {
        this.message.set(`Status changed to ${status}.`);
        this.busy.set(false);
        this.load();
      },
      error: (err) => this.fail(err, 'Could not change the status.')
    });
  }

  protected cancel(): void {
    const reason = this.cancelForm.value.reason;
    if (!reason) {
      return;
    }

    this.busy.set(true);
    this.api.cancelOrder(this.orderId, reason).subscribe({
      next: () => {
        this.message.set('Order cancelled and refunded.');
        this.busy.set(false);
        this.load();
      },
      error: (err) => this.fail(err, 'Could not cancel the order.')
    });
  }

  private fail(err: any, fallback: string): void {
    this.error.set(err?.error?.detail ?? err?.error?.code ?? fallback);
    this.busy.set(false);
  }

  private load(): void {
    this.api
      .getOrder(this.orderId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (order) => {
          this.order.set(order);

          // Pre-fill the delivery quantities with what is still outstanding.
          const remaining: Record<string, number> = {};
          for (const line of order.lines) {
            remaining[line.id] = Math.max(0, line.quantity - line.deliveredPieces);
          }
          this.quantities.set(remaining);

          this.paymentForm.patchValue({
            amountRupees: Math.max(0, order.finalAmountPaise - order.paidPaise) / 100
          });
        },
        error: (err) => this.error.set(err?.error?.detail ?? 'Could not load the order.')
      });

    this.api.listDeliveries(this.orderId).subscribe({
      next: (deliveries) => this.deliveries.set(deliveries),
      error: () => undefined
    });

    this.api.listPayments(this.orderId).subscribe({
      next: (payments) => this.payments.set(payments),
      error: () => undefined
    });
  }
}
