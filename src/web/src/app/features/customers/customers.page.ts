import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Api } from '../../core/api';
import { Customer } from '../../core/models';

@Component({
  selector: 'app-customers',
  imports: [FormsModule],
  template: `
    <section class="card">
      <h1>Customers</h1>

      <form class="form" (ngSubmit)="create()">
        <label>
          Name *
          <input name="name" [(ngModel)]="draft.name" required placeholder="Ravi Jewellers" />
        </label>
        <label>
          Phone *
          <input name="phone" [(ngModel)]="draft.phone" required inputmode="tel" placeholder="9876543210" />
        </label>
        <label>
          Address *
          <input name="address" [(ngModel)]="draft.address" required placeholder="12 Mount Road, Chennai" />
        </label>
        <label>
          PAN
          <input name="pan" [(ngModel)]="draft.pan" maxlength="10" placeholder="ABCDE1234F" />
        </label>
        <button class="button" type="submit" [disabled]="saving() || !canSave()">
          {{ saving() ? 'Saving…' : 'Add customer' }}
        </button>
      </form>

      @if (error()) {
        <p class="card--error">{{ error() }}</p>
      }

      @if (customers().length === 0) {
        <p class="muted">No customers yet.</p>
      } @else {
        <ul class="list">
          @for (customer of customers(); track customer.id) {
            <li>
              <div class="list__main">
                <strong>{{ customer.name }}</strong>
                <span class="muted">{{ customer.phone }} · {{ customer.address }}</span>
              </div>
              <div class="list__side">
                <span class="pill">{{ customer.pan || 'no PAN' }}</span>
              </div>
            </li>
          }
        </ul>
      }
    </section>
  `
})
export class CustomersPage {
  private readonly api = inject(Api);

  protected readonly customers = signal<Customer[]>([]);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected draft = { name: '', phone: '', address: '', pan: '' };

  constructor() {
    this.load();
  }

  protected canSave(): boolean {
    return !!this.draft.name && !!this.draft.phone && !!this.draft.address;
  }

  protected create(): void {
    if (!this.canSave()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    this.api
      .createCustomer({
        name: this.draft.name,
        phone: this.draft.phone,
        address: this.draft.address,
        pan: this.draft.pan || null
      })
      .subscribe({
        next: () => {
          this.draft = { name: '', phone: '', address: '', pan: '' };
          this.saving.set(false);
          this.load();
        },
        error: (err) => {
          this.error.set(err?.error?.code ?? 'Could not create the customer.');
          this.saving.set(false);
        }
      });
  }

  private load(): void {
    this.api.listCustomers().subscribe({
      next: (customers) => this.customers.set(customers),
      error: (err) => this.error.set(err.message ?? 'Could not load customers.')
    });
  }
}
