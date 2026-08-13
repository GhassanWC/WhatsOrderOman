import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PublicStoreService } from '../../core/services/public-store.service';
import { CartService } from '../../core/services/cart.service';
import { AuthService } from '../../core/services/auth.service';
import { AccountApiService } from '../../core/services/account-api.service';
import { BuyerAddressDto, CreatePublicOrderRequest } from '../../core/models/api-types';
import { problemMessage } from '../../shared/components';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

/** One-line summary of a saved address for the free-text delivery field. */
export function addressToText(address: BuyerAddressDto): string {
  const parts = [
    address.area, address.street, address.building, address.apartment,
    address.city, address.wilayat, address.governorate,
  ].filter((p): p is string => !!p);
  const text = parts.join(', ');
  return address.notes ? `${text} — ${address.notes}` : text;
}

/** Oman-friendly phone check (backend re-validates and normalizes to +968…). */
export function isLikelyValidPhone(value: string): boolean {
  const cleaned = value.replace(/[\s\-().]/g, '');
  // International numbers need an explicit + or 00 prefix; bare numbers are
  // treated as Omani (8 digits, starting 9/7 mobile or 2 landline).
  return /^(\+[1-9]\d{7,14}|00[1-9]\d{7,13}|0?[279]\d{7}|968[279]\d{7})$/.test(cleaned);
}

@Component({
  selector: 'wo-checkout-page',
  imports: [ReactiveFormsModule, RouterLink, BilingualPipe, OmrPricePipe, TranslatePipe],
  template: `
    <div class="page container">
      <header class="head">
        <a [routerLink]="['/', slug(), 'cart']" class="back">←</a>
        <h1>{{ 'checkout.title' | t }}</h1>
      </header>

      <form [formGroup]="form" (ngSubmit)="submit()" class="layout">
        <div class="form-col">
          <section class="wo-card panel">
            <h2>{{ 'checkout.yourDetails' | t }}</h2>
            <div class="wo-field">
              <label for="name">{{ 'checkout.name' | t }}</label>
              <input id="name" formControlName="customerName" autocomplete="name" />
            </div>
            <div class="wo-field">
              <label for="phone">{{ 'checkout.phone' | t }}</label>
              <input id="phone" formControlName="customerPhone" inputmode="tel" autocomplete="tel"
                     placeholder="9123 4567" dir="ltr"
                     [class.invalid]="phoneInvalid()" />
              @if (phoneInvalid()) {
                <span class="error">{{ 'checkout.invalidPhone' | t }}</span>
              } @else {
                <span class="hint">{{ 'checkout.phoneHint' | t }}</span>
              }
            </div>
          </section>

          <section class="wo-card panel">
            <h2>{{ 'checkout.fulfillment' | t }}</h2>
            <div class="methods">
              @if (cart.store()?.pickupEnabled) {
                <button type="button" class="method" [class.method--on]="method() === 'Pickup'"
                        (click)="setMethod('Pickup')">🛍️ {{ 'checkout.pickup' | t }}</button>
              }
              @if (cart.store()?.deliveryEnabled) {
                <button type="button" class="method" [class.method--on]="method() === 'Delivery'"
                        (click)="setMethod('Delivery')">
                  🛵 {{ 'checkout.delivery' | t }}
                  @if (cart.deliveryFee() > 0) { <small>+{{ cart.deliveryFee() | omr }}</small> }
                </button>
              }
            </div>

            @if (method() === 'Delivery') {
              @if (savedAddresses().length > 0) {
                <div class="saved-addresses">
                  <span class="hint">{{ 'checkout.savedAddresses' | t }}</span>
                  <div class="addr-chips">
                    @for (address of savedAddresses(); track address.id) {
                      <button type="button" class="addr-chip"
                              [class.on]="selectedAddressId() === address.id"
                              (click)="applyAddress(address)">
                        📍 {{ address.label }}
                        @if (address.isDefault) { <small>★</small> }
                      </button>
                    }
                  </div>
                </div>
              }
              <div class="wo-field">
                <label for="address">{{ 'checkout.address' | t }}</label>
                <textarea id="address" formControlName="deliveryAddress" rows="2"></textarea>
              </div>
              <div class="wo-field">
                <label for="maps">{{ 'checkout.mapsLink' | t }}</label>
                <input id="maps" formControlName="googleMapsUrl" inputmode="url" dir="ltr"
                       placeholder="https://maps.app.goo.gl/…" />
                <span class="hint">{{ 'checkout.mapsLinkHint' | t }}</span>
              </div>
            }

            <div class="wo-field">
              <label for="time">{{ 'checkout.preferredTime' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
              <input id="time" formControlName="preferredTime" [placeholder]="'checkout.preferredTimeHint' | t" />
            </div>
            <div class="wo-field">
              <label for="notes">{{ 'checkout.notes' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
              <textarea id="notes" formControlName="notes" rows="2"></textarea>
            </div>
          </section>
        </div>

        <aside class="summary-col">
          <section class="wo-card panel">
            <h2>{{ 'checkout.summary' | t }}</h2>
            <ul class="items">
              @for (line of cart.lines(); track line.key) {
                <li>
                  <span class="qty">{{ line.quantity }}×</span>
                  <span class="iname">{{ line.name | bilingual: line.nameAr }}
                    @if (line.optionsLabel) { <small class="muted">{{ line.optionsLabel | bilingual: line.optionsLabelAr }}</small> }
                  </span>
                  <span class="iprice">{{ line.unitPrice * line.quantity | omr }}</span>
                </li>
              }
            </ul>
            <div class="totals">
              <div><span>{{ 'cart.subtotal' | t }}</span><span>{{ cart.subtotal() | omr }}</span></div>
              @if (method() === 'Delivery' && cart.deliveryFee() > 0) {
                <div><span>{{ 'cart.deliveryFee' | t }}</span><span>{{ cart.deliveryFee() | omr }}</span></div>
              }
              @if (cart.savings() > 0) {
                <div class="save"><span>{{ 'cart.savings' | t }}</span><span>−{{ cart.savings() | omr }}</span></div>
              }
              <div class="grand"><span>{{ 'cart.total' | t }}</span><span>{{ total() | omr }}</span></div>
            </div>

            @if (error()) { <p class="error-banner">{{ error() }}</p> }

            <button class="wo-btn wo-btn--whatsapp place" type="submit" [disabled]="form.invalid || busy() || cart.count() === 0">
              {{ (busy() ? 'checkout.placing' : 'checkout.placeOrder') | t }}
            </button>
          </section>
        </aside>
      </form>
    </div>
  `,
  styles: `
    .page { padding-block: 18px 60px; }
    .head { display: flex; align-items: center; gap: 12px; margin-bottom: 18px;
      h1 { font-size: 22px; }
      .back { font-size: 22px; color: var(--wo-ink-soft); } }
    .layout { display: grid; grid-template-columns: 1.4fr 1fr; gap: 16px; align-items: start;
      @media (max-width: 820px) { grid-template-columns: 1fr; } }
    .form-col { display: grid; gap: 14px; min-width: 0; }
    .panel { padding: 20px; display: grid; gap: 14px;
      h2 { font-size: 16px; } }
    .methods { display: flex; gap: 10px; flex-wrap: wrap; }
    .method { border: 1.5px solid var(--wo-border); background: var(--wo-surface); border-radius: 12px;
      padding: 12px 20px; font: 600 14.5px var(--wo-font); cursor: pointer; display: inline-flex; gap: 8px;
      align-items: center;
      small { color: var(--wo-ink-soft); font-weight: 500; }
      &.method--on { border-color: var(--wo-primary); background: var(--wo-primary-soft); color: var(--wo-primary-dark); } }
    .summary-col { position: sticky; top: 16px; }
    .items { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px;
      li { display: flex; gap: 8px; font-size: 14px; align-items: baseline; }
      .qty { font-weight: 700; color: var(--wo-primary-dark); }
      .iname { flex: 1; small { display: block; font-size: 12px; } }
      .iprice { font-weight: 600; white-space: nowrap; } }
    .totals { border-top: 1px dashed var(--wo-border); padding-top: 10px; display: grid; gap: 5px;
      div { display: flex; justify-content: space-between; font-size: 14px; color: var(--wo-ink-soft); }
      .save { color: #15803d; }
      .grand { font-weight: 800; color: var(--wo-ink); font-size: 16.5px; } }
    .place { width: 100%; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
    .saved-addresses { display: grid; gap: 6px; }
    .addr-chips { display: flex; gap: 8px; flex-wrap: wrap; }
    .addr-chip { border: 1.5px solid var(--wo-border); background: var(--wo-surface); border-radius: 999px;
      padding: 7px 14px; font: 600 13px var(--wo-font); cursor: pointer;
      small { color: #f59e0b; }
      &.on { border-color: var(--wo-primary); background: var(--wo-primary-soft); color: var(--wo-primary-dark); } }
  `,
})
export class CheckoutPage {
  readonly slug = input.required<string>();

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(PublicStoreService);
  private readonly auth = inject(AuthService);
  private readonly account = inject(AccountApiService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslationService);
  readonly cart = inject(CartService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly method = signal<'Pickup' | 'Delivery'>('Pickup');
  readonly savedAddresses = signal<BuyerAddressDto[]>([]);
  readonly selectedAddressId = signal<string | null>(null);

  readonly total = computed(() =>
    this.cart.subtotal() + (this.method() === 'Delivery' ? this.cart.deliveryFee() : 0));

  readonly form = this.fb.nonNullable.group({
    customerName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    customerPhone: ['', [Validators.required, phoneValidator]],
    deliveryAddress: [''],
    googleMapsUrl: [''],
    preferredTime: ['', Validators.maxLength(100)],
    notes: ['', Validators.maxLength(500)],
  });

  constructor() {
    effect(() => {
      const slug = this.slug();
      if (slug && this.cart.store()?.slug !== slug) {
        this.api.getStore(slug).subscribe((store) => {
          this.cart.openStore(store);
          this.applyStoreMethods();
        });
      } else if (this.cart.store()) {
        this.applyStoreMethods();
      }
    });

    // Signed-in buyers get their details prefilled and saved addresses offered.
    if (this.auth.isAuthenticated()) {
      const name = this.auth.user()?.displayName;
      if (name) this.form.patchValue({ customerName: name });
      this.account.profile().subscribe({
        next: (profile) => {
          if (profile.phone && !this.form.controls.customerPhone.value) {
            this.form.patchValue({ customerPhone: profile.phone });
          }
        },
        error: () => undefined,
      });
      this.account.addresses().subscribe({
        next: (addresses) => this.savedAddresses.set(addresses),
        error: () => undefined,
      });
    }
  }

  applyAddress(address: BuyerAddressDto): void {
    this.selectedAddressId.set(address.id);
    this.form.patchValue({
      deliveryAddress: addressToText(address),
      customerName: address.recipientName,
      customerPhone: address.phone,
    });
  }

  private applyStoreMethods(): void {
    const store = this.cart.store();
    if (!store) return;
    if (!store.pickupEnabled && store.deliveryEnabled) this.setMethod('Delivery');
    else if (!store.deliveryEnabled) this.setMethod('Pickup');
  }

  phoneInvalid(): boolean {
    const control = this.form.controls.customerPhone;
    return control.touched && control.invalid && control.value.length > 0;
  }

  setMethod(method: 'Pickup' | 'Delivery'): void {
    this.method.set(method);
    const address = this.form.controls.deliveryAddress;
    if (method === 'Delivery') {
      address.addValidators([Validators.required, Validators.maxLength(300)]);
    } else {
      address.clearValidators();
    }
    address.updateValueAndValidity();
  }

  submit(): void {
    if (this.form.invalid || this.busy() || this.cart.count() === 0) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();

    const request: CreatePublicOrderRequest = {
      customerName: value.customerName.trim(),
      customerPhone: value.customerPhone.trim(),
      fulfillmentMethod: this.method(),
      deliveryAddress: this.method() === 'Delivery' ? value.deliveryAddress.trim() : null,
      googleMapsUrl: value.googleMapsUrl.trim() || null,
      preferredTime: value.preferredTime.trim() || null,
      notes: value.notes.trim() || null,
      items: this.cart.lines().map((line) => ({
        productId: line.productId,
        quantity: line.quantity,
        optionIds: line.optionIds.length > 0 ? line.optionIds : null,
      })),
    };

    this.api.createOrder(this.slug(), request).subscribe({
      next: (created) => {
        this.cart.clear();
        void this.router.navigate(['/', this.slug(), 'order', created.orderNumber], {
          queryParams: { phone: value.customerPhone.trim() },
          state: { order: created },
        });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  private describeError(err: unknown): string {
    const raw = problemMessage(err, '');
    const code = (err as { error?: { code?: string } })?.error?.code;
    if (code) {
      const translated = this.i18n.t('errors.' + code);
      if (translated !== 'errors.' + code) return translated;
    }
    return raw || this.i18n.t('common.error');
  }
}

function phoneValidator(control: { value: string }): Record<string, boolean> | null {
  return control.value && isLikelyValidPhone(control.value) ? null : { phone: true };
}
