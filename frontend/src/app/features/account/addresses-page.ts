import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountApiService } from '../../core/services/account-api.service';
import { BuyerAddressDto, SaveAddressRequest } from '../../core/models/api-types';
import { EmptyState, problemMessage } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

const GOVERNORATES = [
  'Muscat', 'Dhofar', 'Musandam', 'Al Buraimi', 'Ad Dakhiliyah',
  'Al Batinah North', 'Al Batinah South', 'Ash Sharqiyah North',
  'Ash Sharqiyah South', 'Ad Dhahirah', 'Al Wusta',
];

@Component({
  selector: 'wo-account-addresses',
  imports: [ReactiveFormsModule, TranslatePipe, EmptyState],
  template: `
    <div class="head">
      <h1>{{ 'account.nav.addresses' | t }}</h1>
      @if (!formOpen()) {
        <button class="wo-btn wo-btn--primary" (click)="openCreate()">＋ {{ 'account.addAddress' | t }}</button>
      }
    </div>

    @if (formOpen()) {
      <form class="wo-card form" [formGroup]="form" (ngSubmit)="save()">
        <h2>{{ (editing() ? 'account.editAddress' : 'account.addAddress') | t }}</h2>
        <div class="two">
          <div class="wo-field">
            <label>{{ 'account.addr.label' | t }}</label>
            <input formControlName="label" [placeholder]="'account.addr.labelHint' | t" />
          </div>
          <div class="wo-field">
            <label>{{ 'account.addr.recipient' | t }}</label>
            <input formControlName="recipientName" autocomplete="name" />
          </div>
        </div>
        <div class="two">
          <div class="wo-field">
            <label>{{ 'checkout.phone' | t }}</label>
            <input formControlName="phone" inputmode="tel" dir="ltr" placeholder="9123 4567" />
          </div>
          <div class="wo-field">
            <label>{{ 'account.addr.governorate' | t }}</label>
            <select formControlName="governorate">
              <option value="">—</option>
              @for (gov of governorates; track gov) {
                <option [value]="gov">{{ gov }}</option>
              }
            </select>
          </div>
        </div>
        <div class="two">
          <div class="wo-field">
            <label>{{ 'account.addr.wilayat' | t }}</label>
            <input formControlName="wilayat" />
          </div>
          <div class="wo-field">
            <label>{{ 'account.addr.city' | t }}</label>
            <input formControlName="city" />
          </div>
        </div>
        <div class="two">
          <div class="wo-field">
            <label>{{ 'account.addr.area' | t }}</label>
            <input formControlName="area" />
          </div>
          <div class="wo-field">
            <label>{{ 'account.addr.street' | t }}</label>
            <input formControlName="street" />
          </div>
        </div>
        <div class="two">
          <div class="wo-field">
            <label>{{ 'account.addr.building' | t }}</label>
            <input formControlName="building" />
          </div>
          <div class="wo-field">
            <label>{{ 'account.addr.apartment' | t }}</label>
            <input formControlName="apartment" />
          </div>
        </div>
        <div class="wo-field">
          <label>{{ 'account.addr.notes' | t }}</label>
          <textarea formControlName="notes" rows="2"></textarea>
        </div>
        <label class="default-check">
          <input type="checkbox" formControlName="isDefault" />
          {{ 'account.addr.makeDefault' | t }}
        </label>
        @if (error()) { <p class="error-banner">{{ error() }}</p> }
        <div class="form-actions">
          <button type="submit" class="wo-btn wo-btn--primary" [disabled]="form.invalid || busy()">
            {{ 'common.save' | t }}
          </button>
          <button type="button" class="wo-btn wo-btn--ghost" (click)="closeForm()">{{ 'common.cancel' | t }}</button>
        </div>
      </form>
    }

    @if (!loaded()) {
      <div class="wo-skeleton" style="height: 90px; margin-bottom: 10px"></div>
    } @else if (addresses().length === 0 && !formOpen()) {
      <wo-empty-state emoji="📍" messageKey="account.noAddresses" />
    } @else {
      <div class="list">
        @for (address of addresses(); track address.id) {
          <div class="row wo-card">
            <div class="mid">
              <div class="row1">
                <b>{{ address.label }}</b>
                @if (address.isDefault) { <span class="def">★ {{ 'account.addr.default' | t }}</span> }
              </div>
              <small class="muted">{{ address.recipientName }} · <span dir="ltr">{{ address.phone }}</span></small>
              <small class="muted">{{ describe(address) }}</small>
            </div>
            <div class="actions">
              @if (!address.isDefault) {
                <button class="wo-btn wo-btn--ghost mini" (click)="setDefault(address)">★</button>
              }
              <button class="wo-btn wo-btn--ghost mini" (click)="openEdit(address)">{{ 'common.edit' | t }}</button>
              <button class="wo-btn wo-btn--danger mini" (click)="remove(address)">✕</button>
            </div>
          </div>
        }
      </div>
    }
  `,
  styles: `
    .head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px;
      h1 { margin: 0; font-size: 22px; font-weight: 800; } }
    .form { padding: 18px; display: grid; gap: 12px; margin-bottom: 16px;
      h2 { margin: 0; font-size: 16px; } }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px;
      @media (max-width: 560px) { grid-template-columns: 1fr; } }
    select { width: 100%; border: 1.5px solid var(--wo-border); border-radius: var(--wo-radius-sm);
      background: var(--wo-surface); padding: 10px 12px; font: inherit; outline: none; }
    .default-check { display: flex; align-items: center; gap: 8px; font-size: 14px; }
    .form-actions { display: flex; gap: 10px; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
    .list { display: grid; gap: 10px; }
    .row { display: flex; align-items: center; gap: 12px; padding: 14px 16px; flex-wrap: wrap; }
    .mid { flex: 1; min-width: 200px; display: grid; gap: 2px; }
    .row1 { display: flex; gap: 10px; align-items: center;
      b { font-size: 15px; } }
    .def { color: #b45309; font-size: 12px; font-weight: 700; }
    .actions { display: flex; gap: 6px; }
    .mini { padding: 6px 10px; font-size: 12.5px; }
  `,
})
export class AccountAddressesPage {
  private readonly api = inject(AccountApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);

  protected readonly governorates = GOVERNORATES;
  readonly loaded = signal(false);
  readonly addresses = signal<BuyerAddressDto[]>([]);
  readonly formOpen = signal(false);
  readonly editing = signal<BuyerAddressDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required, Validators.maxLength(40)]],
    recipientName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', Validators.required],
    governorate: [''],
    wilayat: [''],
    city: [''],
    area: [''],
    street: [''],
    building: [''],
    apartment: [''],
    notes: [''],
    isDefault: [false],
  });

  constructor() {
    this.load();
  }

  describe(address: BuyerAddressDto): string {
    return [address.area, address.street, address.building, address.apartment,
      address.city, address.wilayat, address.governorate]
      .filter((p) => !!p)
      .join(', ');
  }

  openCreate(): void {
    this.editing.set(null);
    this.form.reset({ isDefault: this.addresses().length === 0 });
    this.formOpen.set(true);
    this.error.set(null);
  }

  openEdit(address: BuyerAddressDto): void {
    this.editing.set(address);
    this.form.reset({
      label: address.label,
      recipientName: address.recipientName,
      phone: address.phone,
      governorate: address.governorate ?? '',
      wilayat: address.wilayat ?? '',
      city: address.city ?? '',
      area: address.area ?? '',
      street: address.street ?? '',
      building: address.building ?? '',
      apartment: address.apartment ?? '',
      notes: address.notes ?? '',
      isDefault: address.isDefault,
    });
    this.formOpen.set(true);
    this.error.set(null);
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.editing.set(null);
  }

  save(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const request: SaveAddressRequest = {
      label: value.label.trim(),
      recipientName: value.recipientName.trim(),
      phone: value.phone.trim(),
      governorate: value.governorate || null,
      wilayat: value.wilayat.trim() || null,
      city: value.city.trim() || null,
      area: value.area.trim() || null,
      street: value.street.trim() || null,
      building: value.building.trim() || null,
      apartment: value.apartment.trim() || null,
      notes: value.notes.trim() || null,
      latitude: null,
      longitude: null,
      isDefault: value.isDefault,
    };

    const editing = this.editing();
    const call = editing
      ? this.api.updateAddress(editing.id, request)
      : this.api.createAddress(request);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.closeForm();
        this.load();
        this.snackBar.open(this.i18n.t('common.saved'), undefined, { duration: 2000 });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err, this.i18n.t('common.error')));
      },
    });
  }

  setDefault(address: BuyerAddressDto): void {
    this.api.setDefaultAddress(address.id).subscribe({ next: () => this.load() });
  }

  remove(address: BuyerAddressDto): void {
    this.api.deleteAddress(address.id).subscribe({ next: () => this.load() });
  }

  private load(): void {
    this.api.addresses().subscribe({
      next: (addresses) => {
        this.addresses.set(addresses);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}
