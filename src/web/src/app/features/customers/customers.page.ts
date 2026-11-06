import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Api } from '../../core/api';
import { Customer } from '../../core/models';

@Component({
  selector: 'app-customers',
  imports: [FormsModule],
  templateUrl: './customers.page.html'
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
