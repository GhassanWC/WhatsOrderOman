import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { StoreApiService } from '../../core/services/store-api.service';
import { OfferDto, OfferType, SaveOfferRequest } from '../../core/models/api-types';
import { EmptyState, problemMessage } from '../../shared/components';
import { OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

/** Seller offer management: percentage / fixed / free-delivery promotions. */
@Component({
  selector: 'wo-seller-offers-page',
  imports: [ReactiveFormsModule, DatePipe, TranslatePipe, OmrPricePipe, EmptyState],
  template: `
    <div class="head">
      <h1>{{ 'offers.title' | t }}</h1>
      @if (!formOpen()) {
        <button class="wo-btn wo-btn--primary" (click)="openCreate()">＋ {{ 'offers.create' | t }}</button>
      }
    </div>
    <p class="sub muted">{{ 'offers.subtitle' | t }}</p>

    @if (formOpen()) {
      <form class="wo-card form" [formGroup]="form" (ngSubmit)="save()">
        <h2>{{ (editing() ? 'offers.edit' : 'offers.create') | t }}</h2>
        <div class="types">
          @for (type of types; track type) {
            <button type="button" class="type" [class.on]="form.controls.type.value === type"
                    (click)="form.controls.type.setValue(type)">
              @switch (type) {
                @case ('Percentage') { % {{ 'offers.type.Percentage' | t }} }
                @case ('FixedAmount') { − {{ 'offers.type.FixedAmount' | t }} }
                @case ('FreeDelivery') { 🛵 {{ 'offers.type.FreeDelivery' | t }} }
              }
            </button>
          }
        </div>
        <div class="two">
          <div class="wo-field">
            <label>{{ 'offers.titleField' | t }}</label>
            <input formControlName="title" maxlength="100" />
          </div>
          <div class="wo-field">
            <label>{{ 'offers.titleFieldAr' | t }}</label>
            <input formControlName="titleAr" maxlength="100" dir="rtl" />
          </div>
        </div>
        <div class="two">
          @if (form.controls.type.value !== 'FreeDelivery') {
            <div class="wo-field">
              <label>
                {{ (form.controls.type.value === 'Percentage' ? 'offers.percentOff' : 'offers.amountOff') | t }}
              </label>
              <input formControlName="discountValue" type="number" min="0" step="0.001" dir="ltr" />
            </div>
          }
          <div class="wo-field">
            <label>{{ 'offers.minimumOrder' | t }}</label>
            <input formControlName="minimumOrderAmount" type="number" min="0" step="0.001" dir="ltr" />
          </div>
        </div>
        <div class="two">
          <div class="wo-field">
            <label>{{ 'offers.startsAt' | t }}</label>
            <input formControlName="startsAt" type="date" dir="ltr" />
          </div>
          <div class="wo-field">
            <label>{{ 'offers.endsAt' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
            <input formControlName="endsAt" type="date" dir="ltr" />
          </div>
        </div>
        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          {{ 'offers.active' | t }}
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
      <div class="wo-skeleton" style="height: 80px; margin-bottom: 10px"></div>
    } @else if (offers().length === 0 && !formOpen()) {
      <wo-empty-state emoji="🏷️" messageKey="offers.empty">
        <button class="wo-btn wo-btn--primary" (click)="openCreate()">＋ {{ 'offers.create' | t }}</button>
      </wo-empty-state>
    } @else {
      <div class="list">
        @for (offer of offers(); track offer.id) {
          <div class="row wo-card" [class.inactive]="!offer.isRunning">
            <span class="tag">
              @switch (offer.type) {
                @case ('Percentage') { -{{ offer.discountValue }}% }
                @case ('FixedAmount') { -{{ offer.discountValue | omr }} }
                @case ('FreeDelivery') { 🛵 }
              }
            </span>
            <div class="mid">
              <b>{{ offer.title }}</b>
              <small class="muted">
                {{ offer.startsAt | date: 'mediumDate' }}
                – {{ offer.endsAt ? (offer.endsAt | date: 'mediumDate') : '∞' }}
                @if (offer.minimumOrderAmount > 0) { · {{ 'offers.minShort' | t }} {{ offer.minimumOrderAmount | omr }} }
              </small>
            </div>
            <span class="status" [class.live]="offer.isRunning">
              {{ (offer.isRunning ? 'offers.running' : 'offers.notRunning') | t }}
            </span>
            <div class="actions">
              <button class="wo-btn wo-btn--ghost mini" (click)="openEdit(offer)">{{ 'common.edit' | t }}</button>
              <button class="wo-btn wo-btn--danger mini" (click)="remove(offer)">✕</button>
            </div>
          </div>
        }
      </div>
    }
  `,
  styles: `
    .head { display: flex; justify-content: space-between; align-items: center;
      h1 { margin: 0; font-size: 22px; } }
    .sub { margin: 4px 0 16px; font-size: 14px; }
    .form { padding: 18px; display: grid; gap: 12px; margin-bottom: 16px; max-width: 680px;
      h2 { margin: 0; font-size: 16px; } }
    .types { display: flex; gap: 8px; flex-wrap: wrap; }
    .type { border: 1.5px solid var(--wo-border); background: var(--wo-surface); border-radius: 10px;
      padding: 9px 16px; font: 600 13.5px var(--wo-font); cursor: pointer;
      &.on { border-color: var(--wo-primary); background: var(--wo-primary-soft); color: var(--wo-primary-dark); } }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px;
      @media (max-width: 560px) { grid-template-columns: 1fr; } }
    .check { display: flex; align-items: center; gap: 8px; font-size: 14px; }
    .form-actions { display: flex; gap: 10px; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
    .list { display: grid; gap: 10px; }
    .row { display: flex; align-items: center; gap: 12px; padding: 14px 16px; flex-wrap: wrap; }
    .row.inactive { opacity: .65; }
    .tag { min-width: 56px; height: 44px; display: grid; place-items: center; border-radius: 10px;
      background: linear-gradient(120deg, #e11d48, #be123c); color: #fff; font-weight: 800; direction: ltr;
      padding-inline: 8px; }
    .mid { flex: 1; min-width: 180px; display: grid; gap: 2px;
      b { font-size: 15px; } }
    .status { font-size: 12.5px; font-weight: 700; color: var(--wo-muted); }
    .status.live { color: var(--wo-primary-dark); }
    .actions { display: flex; gap: 6px; }
    .mini { padding: 6px 10px; font-size: 12.5px; }
  `,
})
export class SellerOffersPage {
  private readonly api = inject(StoreApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);

  protected readonly types: OfferType[] = ['Percentage', 'FixedAmount', 'FreeDelivery'];
  readonly loaded = signal(false);
  readonly offers = signal<OfferDto[]>([]);
  readonly formOpen = signal(false);
  readonly editing = signal<OfferDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(100)]],
    titleAr: [''],
    type: ['Percentage' as OfferType, Validators.required],
    discountValue: [10],
    minimumOrderAmount: [0],
    startsAt: [todayIso()],
    endsAt: [''],
    isActive: [true],
  });

  constructor() {
    this.load();
  }

  openCreate(): void {
    this.editing.set(null);
    this.form.reset({
      title: '', titleAr: '', type: 'Percentage', discountValue: 10,
      minimumOrderAmount: 0, startsAt: todayIso(), endsAt: '', isActive: true,
    });
    this.formOpen.set(true);
    this.error.set(null);
  }

  openEdit(offer: OfferDto): void {
    this.editing.set(offer);
    this.form.reset({
      title: offer.title,
      titleAr: offer.titleAr ?? '',
      type: offer.type,
      discountValue: offer.discountValue,
      minimumOrderAmount: offer.minimumOrderAmount,
      startsAt: offer.startsAt.slice(0, 10),
      endsAt: offer.endsAt?.slice(0, 10) ?? '',
      isActive: offer.isActive,
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
    const request: SaveOfferRequest = {
      title: value.title.trim(),
      titleAr: value.titleAr.trim() || null,
      description: null,
      descriptionAr: null,
      type: value.type,
      discountValue: value.type === 'FreeDelivery' ? 0 : Number(value.discountValue),
      minimumOrderAmount: Number(value.minimumOrderAmount) || 0,
      startsAt: new Date(value.startsAt + 'T00:00:00Z').toISOString(),
      endsAt: value.endsAt ? new Date(value.endsAt + 'T23:59:59Z').toISOString() : null,
      isActive: value.isActive,
    };

    const editing = this.editing();
    const call = editing ? this.api.updateOffer(editing.id, request) : this.api.createOffer(request);
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

  remove(offer: OfferDto): void {
    this.api.deleteOffer(offer.id).subscribe({ next: () => this.load() });
  }

  private load(): void {
    this.api.getOffers().subscribe({
      next: (offers) => {
        this.offers.set(offers);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}
