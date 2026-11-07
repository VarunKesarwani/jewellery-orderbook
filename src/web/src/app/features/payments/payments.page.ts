import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';

import { Api } from '../../core/api';
import { Order, Payment } from '../../core/models';
import { InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-payments',
  imports: [ReactiveFormsModule, InrPipe],
  templateUrl: './payments.page.html'
})
export class PaymentsPage {
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly orders = signal<Order[]>([]);
  protected readonly todaysPayments = signal<Payment[]>([]);
  protected readonly search = signal('');
  protected readonly selectedId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);

  protected readonly form = this.fb.group({
    amountRupees: [0, [Validators.min(0)]],
    paidOn: [this.today, Validators.required],
    method: ['UPI'],
    reference: ['']
  });

  /** Orders with money still owed, newest first. */
  protected readonly openOrders = computed(() => {
    const term = this.search().trim().toLowerCase();

    return this.orders()
      .filter((o) => o.status !== 'Cancelled' && o.finalAmountPaise - o.paidPaise > 0)
      .filter(
        (o) =>
          !term ||
          String(o.number).includes(term) ||
          o.customerName.toLowerCase().includes(term)
      );
  });

  protected readonly selected = computed(
    () => this.orders().find((o) => o.id === this.selectedId()) ?? null
  );

  protected readonly balance = computed(() => {
    const order = this.selected();
    return order ? Math.max(0, order.finalAmountPaise - order.paidPaise) : 0;
  });

  protected readonly collectedToday = computed(() =>
    this.todaysPayments().reduce((sum, p) => sum + (p.isRefund ? -p.amountPaise : p.amountPaise), 0)
  );

  constructor() {
    const preselected = this.route.snapshot.queryParamMap.get('orderId');
    if (preselected) {
      this.selectedId.set(preselected);
    }

    this.load();
  }

  protected onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  protected select(order: Order): void {
    this.selectedId.set(order.id);
    this.form.patchValue({ amountRupees: Math.max(0, order.finalAmountPaise - order.paidPaise) / 100 });
    this.message.set(null);
    this.error.set(null);
  }

  protected record(): void {
    const order = this.selected();
    const amount = Math.round((Number(this.form.value.amountRupees) || 0) * 100);

    if (!order) {
      this.error.set('Pick an order first.');
      return;
    }

    if (amount <= 0) {
      this.error.set('Enter an amount greater than zero.');
      return;
    }

    if (amount > this.balance()) {
      this.error.set(`That is more than the balance of ${(this.balance() / 100).toFixed(2)}.`);
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    this.api
      .createPayment({
        orderId: order.id,
        amountPaise: amount,
        paidOn: this.form.value.paidOn ?? this.today,
        method: this.form.value.method || null,
        reference: this.form.value.reference || null
      })
      .subscribe({
        next: (payment) => {
          this.message.set(`Payment #${payment.number} recorded against order #${order.number}.`);
          this.form.patchValue({ amountRupees: 0, reference: '' });
          this.busy.set(false);
          this.load();
        },
        error: (err) => {
          this.error.set(err?.error?.detail ?? err?.error?.code ?? 'Could not record the payment.');
          this.busy.set(false);
        }
      });
  }

  private load(): void {
    forkJoin({
      orders: this.api.listOrders(),
      payments: this.api.paymentsForDate(this.form.value.paidOn ?? this.today)
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.orders.set(result.orders);
          this.todaysPayments.set(result.payments);

          // Keep the selection sensible after a reload.
          const current = this.selectedId();
          if (current && result.orders.some((o) => o.id === current)) {
            const order = result.orders.find((o) => o.id === current)!;
            this.form.patchValue({
              amountRupees: Math.max(0, order.finalAmountPaise - order.paidPaise) / 100
            });
          } else if (!current) {
            this.selectedId.set(null);
          }
        },
        error: () => this.error.set('Could not load orders and payments.')
      });
  }
}
