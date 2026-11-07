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
  templateUrl: './order-detail.page.html'
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
  protected readonly showCancel = signal(false);
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


  protected useFullBalance(): void {
    this.paymentForm.patchValue({ amountRupees: this.balance() / 100 });
  }

  protected recordPayment(): void {
    const order = this.order();
    const amount = Math.round((Number(this.paymentForm.value.amountRupees) || 0) * 100);

    if (!order || amount <= 0) {
      this.error.set('Enter an amount greater than zero.');
      return;
    }

    if (amount > this.balance()) {
      this.error.set('That is more than the outstanding balance.');
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
        this.showCancel.set(false);
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
