import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, of, switchMap } from 'rxjs';

import { Api } from '../../core/api';
import { Customer, LineRequest, Order, OrderRequest, StoneRequest, TaxRequest } from '../../core/models';
import { GramsPipe, InrPipe } from '../../shared/format.pipes';

@Component({
  selector: 'app-order-entry',
  imports: [ReactiveFormsModule, RouterLink, InrPipe, GramsPipe],
  template: `
    <section class="card">
      <div class="toolbar">
        <h1>New order</h1>
        <span class="spacer"></span>
        <a class="button button--ghost" routerLink="/orders">Cancel</a>
      </div>

      <form [formGroup]="form" (ngSubmit)="save()">
        <!-- header -->
        <div class="grid-3">
          <label>
            Customer *
            <select formControlName="customerId" required>
              <option value="">Select a customer</option>
              @for (customer of customers(); track customer.id) {
                <option [value]="customer.id">{{ customer.name }}</option>
              }
            </select>
          </label>
          <label>
            Order date
            <input type="date" formControlName="orderDate" />
          </label>
          <label>
            Expected delivery
            <input type="date" formControlName="expectedDeliveryDate" />
          </label>
        </div>

        <!-- tax components, set once for the order and copied to each line -->
        <fieldset class="block" formGroupName="tax">
          <legend>Tax</legend>
          <div class="grid-4">
            @for (component of taxComponents; track component.key) {
              <label>
                {{ component.label }}
                <select [formControlName]="component.key">
                  <option [ngValue]="null">none</option>
                  @for (rate of taxRates; track rate) {
                    <option [ngValue]="rate">{{ rate }}%</option>
                  }
                </select>
              </label>
            }
          </div>
        </fieldset>

        <!-- lines -->
        <div formArrayName="lines">
          @for (line of lines.controls; track line; let i = $index) {
            <fieldset class="block line-card" [formGroupName]="i">
              <legend>Item {{ i + 1 }}</legend>

              <div class="grid-3">
                <label>
                  Item name *
                  <input formControlName="itemName" placeholder="22K ring" />
                </label>
                <label>
                  Description
                  <input formControlName="itemDescription" placeholder="Design 1042" />
                </label>
                <label>
                  Pieces
                  <input type="number" formControlName="quantity" min="1" />
                </label>
              </div>

              <div class="grid-4">
                <label>
                  Metal
                  <select formControlName="metalType">
                    <option value="Gold">Gold</option>
                    <option value="Silver">Silver</option>
                    <option value="Platinum">Platinum</option>
                  </select>
                </label>
                <label>
                  Purity
                  <input formControlName="metalPurity" placeholder="22K" />
                </label>
                <label>
                  Gross weight (g)
                  <input type="number" formControlName="grossWeightGrams" step="0.001" min="0" />
                </label>
                <label>
                  Rate (₹/g)
                  <input type="number" formControlName="ratePerGramRupees" step="0.01" min="0" />
                </label>
              </div>

              <div class="grid-4">
                <label>
                  Wastage %
                  <input type="number" formControlName="wastagePct" step="0.01" min="0" />
                </label>
                <label>
                  Making
                  <select formControlName="makingType">
                    <option value="PerGram">Per gram</option>
                    <option value="Percent">Percent</option>
                    <option value="Fixed">Fixed</option>
                  </select>
                </label>
                <label>
                  Making rate
                  <input type="number" formControlName="makingRateRupees" step="0.01" min="0" />
                </label>
                <label>
                  Other charges (₹)
                  <input type="number" formControlName="otherChargesRupees" step="0.01" min="0" />
                </label>
              </div>

              <div class="grid-4">
                <label>
                  Discount (₹)
                  <input type="number" formControlName="discountRupees" step="0.01" min="0" />
                </label>
                <label>
                  Rate date
                  <input type="date" formControlName="rateDate" />
                </label>
                <div class="spacer"></div>
                <div class="spacer"></div>
              </div>

              <!-- stones -->
              <div formArrayName="stones">
                @for (stone of stonesOf(i).controls; track stone; let s = $index) {
                  <div class="stone-row" [formGroupName]="s">
                    <label>
                      Stone
                      <select formControlName="category">
                        <option value="Diamond">Diamond</option>
                        <option value="Precious">Precious</option>
                        <option value="SemiPrecious">Semi-precious</option>
                      </select>
                    </label>
                    <label>
                      Count
                      <input type="number" formControlName="stoneCount" min="1" />
                    </label>
                    <label>
                      Carats
                      <input type="number" formControlName="totalCarats" step="0.001" min="0" />
                    </label>
                    <label>
                      Value (₹)
                      <input type="number" formControlName="stoneValueRupees" step="0.01" min="0" />
                    </label>
                    <label>
                      Colour
                      <input formControlName="colourGrade" placeholder="G" />
                    </label>
                    <label>
                      Clarity
                      <input formControlName="clarityGrade" placeholder="VVS1" />
                    </label>
                    <label>
                      Lab
                      <input formControlName="certificationLab" placeholder="GIA" />
                    </label>
                    <label>
                      Certificate
                      <input formControlName="certificateNumber" />
                    </label>
                    <button type="button" class="link-button" (click)="removeStone(i, s)">remove</button>
                  </div>
                }
              </div>

              <p class="actions">
                <button type="button" class="button button--ghost" (click)="addStone(i)">Add stone</button>
                @if (lines.length > 1) {
                  <button type="button" class="link-button" (click)="removeLine(i)">Remove item</button>
                }
              </p>

              <!-- what the server says this line costs -->
              @if (previewLine(i); as priced) {
                <dl class="line-totals">
                  <div><dt>Net</dt><dd>{{ priced.netWeightMg | grams }}</dd></div>
                  <div><dt>Wastage</dt><dd>{{ priced.wastageWeightMg | grams }}</dd></div>
                  <div><dt>Metal</dt><dd>{{ priced.metalValuePaise | inr }}</dd></div>
                  <div><dt>Making</dt><dd>{{ priced.makingChargesPaise | inr }}</dd></div>
                  <div><dt>Stones</dt><dd>{{ priced.stoneValuePaise | inr }}</dd></div>
                  <div><dt>Tax</dt><dd>{{ priced.taxPaise | inr }}</dd></div>
                  <div class="strong"><dt>Line total</dt><dd>{{ priced.lineTotalPaise | inr }}</dd></div>
                </dl>
              }
            </fieldset>
          }
        </div>

        <p class="actions">
          <button type="button" class="button button--ghost" (click)="addLine()">Add item</button>
        </p>

        <!-- order totals, also from the server -->
        @if (preview(); as totals) {
          <dl class="order-totals">
            <div><dt>Subtotal</dt><dd>{{ totals.subtotalPaise | inr }}</dd></div>
            <div><dt>Discount</dt><dd>−{{ discountTotal() | inr }}</dd></div>
            <div><dt>Tax</dt><dd>{{ totals.taxPaise | inr }}</dd></div>
            <div><dt>Round off</dt><dd>{{ totals.roundOffPaise | inr }}</dd></div>
            <div class="strong"><dt>Total</dt><dd>{{ totals.finalAmountPaise | inr }}</dd></div>
          </dl>
        }

        @if (error()) {
          <p class="card--error">{{ error() }}</p>
        }

        <p class="actions">
          <button class="button" type="submit" [disabled]="saving() || !canSave()">
            {{ saving() ? 'Saving…' : 'Save order' }}
          </button>
          @if (!canSave()) {
            <span class="muted">Pick a customer and give the first item a name and a weight.</span>
          }
        </p>
      </form>
    </section>
  `
})
export class OrderEntryPage {
  private readonly api = inject(Api);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly taxRates = [3, 5, 9, 12, 18, 28];
  protected readonly taxComponents = [
    { key: 'gold', label: 'Gold' },
    { key: 'wastage', label: 'Wastage' },
    { key: 'making', label: 'Making' },
    { key: 'stone', label: 'Stone' }
  ];

  protected readonly customers = signal<Customer[]>([]);
  protected readonly preview = signal<Order | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly today = new Date().toISOString().slice(0, 10);

  protected readonly form = this.fb.group({
    customerId: ['', Validators.required],
    orderDate: [this.today, Validators.required],
    expectedDeliveryDate: [null as string | null],
    notes: [''],
    tax: this.fb.group({ gold: [null], wastage: [null], making: [null], stone: [null] }),
    lines: this.fb.array([this.newLine()])
  });

  protected get lines(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  // A signal rather than a computed: the form is not a signal source, so a computed
  // would cache its first value and never re-evaluate.
  protected readonly canSave = signal(false);

  constructor() {
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.canSave.set(this.isReadyToSave()));

    this.api.listCustomers().subscribe({
      next: (customers) => this.customers.set(customers),
      error: () => this.error.set('Could not load customers.')
    });

    // Every edit re-prices on the server, debounced. One implementation of the
    // arithmetic, so the preview cannot disagree with what gets stored.
    this.form.valueChanges
      .pipe(
        debounceTime(350),
        switchMap(() => {
          const request = this.toRequest();
          if (!request) {
            return of(null);
          }
          return this.api.calculateOrder(request).pipe(catchError(() => of(null)));
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((priced) => this.preview.set(priced));
  }

  private isReadyToSave(): boolean {
    const value = this.form.getRawValue() as any;
    const first = value.lines?.[0];
    return !!value.customerId && !!first?.itemName && Number(first?.grossWeightGrams) > 0;
  }

  protected stonesOf(lineIndex: number): FormArray {
    return this.lines.at(lineIndex).get('stones') as FormArray;
  }

  protected discountTotal = () => (this.preview()?.lines ?? []).reduce((sum, l) => sum + l.discountPaise, 0);

  protected previewLine(index: number) {
    const priced = this.preview();
    return priced && priced.lines.length > index ? priced.lines[index] : null;
  }

  protected addLine(): void {
    this.lines.push(this.newLine());
  }

  protected removeLine(index: number): void {
    if (this.lines.length > 1) {
      this.lines.removeAt(index);
    }
  }

  protected addStone(lineIndex: number): void {
    this.stonesOf(lineIndex).push(this.newStone());
  }

  protected removeStone(lineIndex: number, stoneIndex: number): void {
    this.stonesOf(lineIndex).removeAt(stoneIndex);
  }

  protected save(): void {
    const request = this.toRequest();
    if (!request || !this.canSave()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    this.api.createOrder(request).subscribe({
      next: (order) => this.router.navigate(['/orders'], { queryParams: { created: order.number } }),
      error: (err) => {
        this.error.set(err?.error?.detail ?? err?.error?.code ?? 'Could not save the order.');
        this.saving.set(false);
      }
    });
  }

  private newLine(): FormGroup {
    return this.fb.group({
      itemName: [''],
      itemDescription: [''],
      quantity: [1, Validators.min(1)],
      metalType: ['Gold'],
      metalPurity: ['22K'],
      grossWeightGrams: [0],
      ratePerGramRupees: [0],
      rateDate: [this.today],
      wastagePct: [0],
      makingType: ['PerGram'],
      makingRateRupees: [0],
      otherChargesRupees: [0],
      discountRupees: [0],
      stones: this.fb.array([])
    });
  }

  private newStone(): FormGroup {
    return this.fb.group({
      category: ['Diamond'],
      stoneCount: [1],
      totalCarats: [0],
      stoneValueRupees: [0],
      colourGrade: [''],
      clarityGrade: [''],
      certificationLab: [''],
      certificateNumber: ['']
    });
  }

  private toTax(value: any): TaxRequest {
    return {
      gold: value?.gold ?? null,
      wastage: value?.wastage ?? null,
      making: value?.making ?? null,
      stone: value?.stone ?? null
    };
  }

  /** Form values are in grams, rupees and percent; the API speaks mg, paise and basis points. */
  private toRequest(): OrderRequest | null {
    const value = this.form.getRawValue() as any;

    const lines: LineRequest[] = (value.lines ?? []).map((line: any) => ({
      itemName: line.itemName || 'Item',
      itemDescription: line.itemDescription || null,
      quantity: Number(line.quantity) || 1,
      metalType: line.metalType,
      metalPurity: line.metalPurity || null,
      grossWeightMg: Math.round((Number(line.grossWeightGrams) || 0) * 1000),
      ratePerGramPaise: Math.round((Number(line.ratePerGramRupees) || 0) * 100),
      rateDate: line.rateDate || value.orderDate,
      wastagePctBp: Math.round((Number(line.wastagePct) || 0) * 100),
      makingType: line.makingType,
      makingRate: Math.round((Number(line.makingRateRupees) || 0) * 100),
      otherChargesPaise: Math.round((Number(line.otherChargesRupees) || 0) * 100),
      discountPaise: Math.round((Number(line.discountRupees) || 0) * 100),
      tax: this.toTax(value.tax),
      stones: (line.stones ?? []).map(
        (stone: any): StoneRequest => ({
          category: stone.category,
          stoneCount: Number(stone.stoneCount) || 1,
          totalCaratsMilli: Math.round((Number(stone.totalCarats) || 0) * 1000),
          stoneValuePaise: Math.round((Number(stone.stoneValueRupees) || 0) * 100),
          colourGrade: stone.colourGrade || null,
          clarityGrade: stone.clarityGrade || null,
          certificationLab: stone.certificationLab || null,
          certificateNumber: stone.certificateNumber || null
        })
      )
    }));

    if (lines.length === 0 || lines.some((l) => l.grossWeightMg <= 0)) {
      return null;
    }

    return {
      customerId: value.customerId || '00000000-0000-0000-0000-000000000000',
      orderDate: value.orderDate,
      expectedDeliveryDate: value.expectedDeliveryDate || null,
      notes: value.notes || null,
      tax: this.toTax(value.tax),
      lines
    };
  }
}
