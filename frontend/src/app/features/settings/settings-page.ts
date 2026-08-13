import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { StoreApiService } from '../../core/services/store-api.service';
import { OpeningHourItem, StoreDto, SubscriptionDto } from '../../core/models/api-types';
import { problemMessage } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'wo-settings-page',
  imports: [ReactiveFormsModule, MatSlideToggleModule, MatTabsModule, TranslatePipe],
  template: `
    <h1 class="title">{{ 'settings.title' | t }}</h1>

    @if (store(); as s) {
      <mat-tab-group>
        <!-- ── Store profile ─────────────────────────────────────────── -->
        <mat-tab [label]="'settings.tabProfile' | t">
          <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="panel">
            <div class="logo-row">
              <div class="logo">
                @if (s.logoUrl) {
                  <img [src]="s.logoUrl" alt="logo" />
                } @else {
                  <span>{{ s.name.charAt(0) }}</span>
                }
              </div>
              <label class="wo-btn wo-btn--ghost small">
                {{ 'settings.changeLogo' | t }}
                <input type="file" accept="image/*" hidden (change)="uploadLogo($event)" />
              </label>
              @if (s.logoUrl) {
                <button type="button" class="wo-btn wo-btn--ghost small danger-text" (click)="removeLogo()">
                  {{ 'common.remove' | t }}
                </button>
              }
            </div>

            <div class="banner-block">
              <label class="field-label">{{ 'settings.banner' | t }}</label>
              <div class="banner-preview" [class.empty]="!s.bannerUrl">
                @if (s.bannerUrl) {
                  <img [src]="s.bannerUrl" alt="banner" />
                } @else {
                  <span class="muted">{{ 'settings.bannerHint' | t }}</span>
                }
              </div>
              <div class="banner-actions">
                <label class="wo-btn wo-btn--ghost small">
                  {{ (s.bannerUrl ? 'settings.replaceBanner' : 'settings.uploadBanner') | t }}
                  <input type="file" accept="image/*" hidden (change)="uploadBanner($event)" />
                </label>
                @if (s.bannerUrl) {
                  <button type="button" class="wo-btn wo-btn--ghost small danger-text" (click)="removeBanner()">
                    {{ 'common.remove' | t }}
                  </button>
                }
              </div>
            </div>

            <div class="row">
              <div class="wo-field">
                <label>{{ 'onboarding.storeName' | t }}</label>
                <input formControlName="name" />
              </div>
              <div class="wo-field">
                <label>{{ 'onboarding.storeNameAr' | t }}</label>
                <input formControlName="nameAr" dir="rtl" />
              </div>
            </div>

            <div class="wo-field">
              <label>{{ 'settings.storeLink' | t }}</label>
              <div class="slug-wrap" dir="ltr"><span>whatsorder.om/</span>
                <input formControlName="slug" autocapitalize="off" spellcheck="false" /></div>
            </div>

            <div class="row">
              <div class="wo-field">
                <label>{{ 'onboarding.whatsapp' | t }} <small class="muted">({{ 'common.optional' | t }})</small></label>
                <input formControlName="whatsAppNumber" inputmode="tel" />
              </div>
              <div class="wo-field">
                <label>{{ 'onboarding.instagram' | t }}</label>
                <input formControlName="instagramHandle" />
              </div>
            </div>

            <div class="row">
              <div class="wo-field">
                <label>{{ 'onboarding.description' | t }}</label>
                <textarea formControlName="description" rows="2"></textarea>
              </div>
              <div class="wo-field">
                <label>{{ 'onboarding.descriptionAr' | t }}</label>
                <textarea formControlName="descriptionAr" rows="2" dir="rtl"></textarea>
              </div>
            </div>

            <div class="row">
              <div class="wo-field">
                <label>{{ 'onboarding.location' | t }}</label>
                <input formControlName="locationText" />
              </div>
              <div class="wo-field">
                <label>{{ 'onboarding.governorate' | t }}</label>
                <input formControlName="governorate" />
              </div>
            </div>

            <mat-slide-toggle formControlName="isAcceptingOrders">
              {{ 'settings.acceptingOrders' | t }}
            </mat-slide-toggle>
            <p class="hint">{{ 'settings.acceptingOrdersHint' | t }}</p>

            @if (error()) { <p class="error-banner">{{ error() }}</p> }
            <button class="wo-btn wo-btn--primary save" type="submit" [disabled]="profileForm.invalid || busy()">
              {{ 'common.save' | t }}
            </button>
          </form>
        </mat-tab>

        <!-- ── Ordering ─────────────────────────────────────────────── -->
        <mat-tab [label]="'settings.tabOrdering' | t">
          <form [formGroup]="orderingForm" (ngSubmit)="saveSettings()" class="panel">
            <div class="row">
              <div class="wo-field">
                <label>{{ 'settings.deliveryFee' | t }}</label>
                <input formControlName="deliveryFee" type="number" step="0.001" min="0" inputmode="decimal" />
              </div>
              <div class="wo-field">
                <label>{{ 'settings.minimumOrder' | t }}</label>
                <input formControlName="minimumOrderAmount" type="number" step="0.001" min="0" inputmode="decimal" />
              </div>
            </div>
            <div class="toggles">
              <mat-slide-toggle formControlName="pickupEnabled">{{ 'settings.pickupEnabled' | t }}</mat-slide-toggle>
              <mat-slide-toggle formControlName="deliveryEnabled">{{ 'settings.deliveryEnabled' | t }}</mat-slide-toggle>
            </div>
            <div class="wo-field lang-field">
              <label>{{ 'settings.storeLanguage' | t }}</label>
              <select formControlName="defaultLanguage">
                <option value="en">English</option>
                <option value="ar">العربية</option>
              </select>
            </div>

            @if (error()) { <p class="error-banner">{{ error() }}</p> }
            <button class="wo-btn wo-btn--primary save" type="submit" [disabled]="busy()">
              {{ 'common.save' | t }}
            </button>
          </form>
        </mat-tab>

        <!-- ── Opening hours ─────────────────────────────────────────── -->
        <mat-tab [label]="'settings.tabHours' | t">
          <div class="panel">
            <div class="hours">
              @for (hour of hours(); track hour.day) {
                <div class="hour-row" [class.hour-row--closed]="hour.closed">
                  <span class="day">{{ 'settings.days.' + hour.day | t }}</span>
                  <mat-slide-toggle [checked]="!hour.closed" (change)="setClosed(hour.day, !$event.checked)">
                    {{ (hour.closed ? 'settings.closed' : 'settings.open') | t }}
                  </mat-slide-toggle>
                  @if (!hour.closed) {
                    <div class="times" dir="ltr">
                      <input type="time" [value]="hour.open" (change)="setTime(hour.day, 'open', $event)" />
                      <span class="muted">{{ 'settings.to' | t }}</span>
                      <input type="time" [value]="hour.close" (change)="setTime(hour.day, 'close', $event)" />
                    </div>
                  }
                </div>
              }
            </div>
            @if (error()) { <p class="error-banner">{{ error() }}</p> }
            <button class="wo-btn wo-btn--primary save" type="button" [disabled]="busy()" (click)="saveSettings()">
              {{ 'common.save' | t }}
            </button>
          </div>
        </mat-tab>

        <!-- ── Plan ─────────────────────────────────────────────────── -->
        <mat-tab [label]="'settings.tabPlan' | t">
          <div class="panel">
            @if (subscription(); as sub) {
              <div class="plan-card wo-card">
                <div class="plan-head">
                  <span class="plan-name">{{ (sub.plan === 'Pro' ? 'settings.plan.pro' : 'settings.plan.free') | t }}</span>
                  <span class="wo-chip" [class.wo-chip--ready]="sub.plan === 'Pro'" [class.wo-chip--new]="sub.plan !== 'Pro'">
                    {{ 'settings.plan.current' | t }}
                  </span>
                </div>
                <ul class="plan-facts">
                  <li>
                    <span>{{ 'settings.plan.productsUsed' | t }}</span>
                    <b>{{ sub.productsUsed }} / {{ sub.maxProducts ?? ('settings.plan.unlimited' | t) }}</b>
                  </li>
                  <li>
                    <span>{{ 'settings.plan.ordersThisMonth' | t }}</span>
                    <b>{{ sub.ordersThisMonth }} / {{ sub.maxOrdersPerMonth ?? ('settings.plan.unlimited' | t) }}</b>
                  </li>
                  <li>
                    <span>{{ 'settings.plan.whatsapp' | t }}</span>
                    <b>{{ (sub.whatsAppNotifications ? 'settings.plan.included' : 'settings.plan.notIncluded') | t }}</b>
                  </li>
                  <li>
                    <span>{{ 'settings.plan.analytics' | t }}</span>
                    <b>{{ (sub.analytics ? 'settings.plan.included' : 'settings.plan.notIncluded') | t }}</b>
                  </li>
                </ul>
                <button class="wo-btn wo-btn--primary" type="button" [disabled]="busy()" (click)="togglePlan(sub)">
                  {{ (sub.plan === 'Pro' ? 'settings.plan.downgrade' : 'settings.plan.upgrade') | t }}
                </button>
                <p class="hint">{{ 'settings.plan.upgradeNote' | t }}</p>
              </div>
            }
          </div>
        </mat-tab>
      </mat-tab-group>
    } @else {
      <div class="wo-skeleton" style="height: 300px"></div>
    }
  `,
  styles: `
    .title { font-size: 22px; margin-bottom: 16px; }
    .panel { padding: 22px 4px; display: grid; gap: 16px; max-width: 720px; }
    .row { display: grid; gap: 14px; grid-template-columns: 1fr 1fr;
      @media (max-width: 640px) { grid-template-columns: 1fr; } }
    .logo-row { display: flex; align-items: center; gap: 16px; }
    .logo { width: 72px; height: 72px; border-radius: 18px; overflow: hidden; background: var(--wo-primary-soft);
      display: grid; place-items: center; font: 800 28px var(--wo-font); color: var(--wo-primary-dark);
      img { width: 100%; height: 100%; object-fit: cover; } }
    .small { padding: 8px 16px; font-size: 13.5px; cursor: pointer; }
    .danger-text { color: var(--wo-danger); }
    .banner-block { display: grid; gap: 8px; }
    .field-label { font-weight: 600; font-size: 14px; color: var(--wo-ink-soft); }
    .banner-preview { aspect-ratio: 3 / 1; border-radius: var(--wo-radius-sm); overflow: hidden;
      background: var(--wo-bg); border: 1.5px dashed var(--wo-border); display: grid; place-items: center;
      img { width: 100%; height: 100%; object-fit: cover; } }
    .banner-preview:not(.empty) { border-style: solid; }
    .banner-actions { display: flex; gap: 8px; }
    .slug-wrap { display: flex; align-items: center; border: 1.5px solid var(--wo-border); border-radius: 10px;
      background: var(--wo-surface); overflow: hidden;
      span { padding-inline: 12px 2px; color: var(--wo-muted); font-weight: 600; font-size: 14px; }
      input { border: none; outline: none; padding: 10px 4px; font: 500 15px var(--wo-font); flex: 1; } }
    .toggles { display: flex; gap: 24px; flex-wrap: wrap; }
    .lang-field { max-width: 340px; }
    select, input, textarea { font-family: var(--wo-font); }
    .hours { display: grid; gap: 10px; }
    .hour-row { display: flex; align-items: center; gap: 16px; padding: 10px 14px; border: 1px solid var(--wo-border);
      border-radius: 12px; background: var(--wo-surface); flex-wrap: wrap;
      .day { font-weight: 700; width: 90px; } }
    .hour-row--closed { opacity: .6; }
    .times { display: flex; align-items: center; gap: 8px;
      input { border: 1.5px solid var(--wo-border); border-radius: 8px; padding: 6px 8px; font: 500 14px var(--wo-font); } }
    .plan-card { padding: 22px; display: grid; gap: 14px; max-width: 460px; }
    .plan-head { display: flex; justify-content: space-between; align-items: center; }
    .plan-name { font-size: 20px; font-weight: 800; }
    .plan-facts { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px;
      li { display: flex; justify-content: space-between; font-size: 14px; color: var(--wo-ink-soft);
        b { color: var(--wo-ink); } } }
    .save { justify-self: start; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
    .hint { color: var(--wo-muted); font-size: 12.5px; }
  `,
})
export class SettingsPage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(StoreApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);

  readonly store = signal<StoreDto | null>(null);
  readonly subscription = signal<SubscriptionDto | null>(null);
  readonly hours = signal<OpeningHourItem[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    nameAr: [''],
    slug: ['', Validators.required],
    whatsAppNumber: [''], // optional — the built-in order chat works without WhatsApp
    instagramHandle: [''],
    description: [''],
    descriptionAr: [''],
    locationText: [''],
    governorate: [''],
    isAcceptingOrders: [true],
  });

  readonly orderingForm = this.fb.nonNullable.group({
    deliveryFee: [0, [Validators.min(0)]],
    minimumOrderAmount: [0, [Validators.min(0)]],
    deliveryEnabled: [true],
    pickupEnabled: [true],
    defaultLanguage: ['en'],
  });

  constructor() {
    this.api.getMyStore().subscribe((store) => this.populate(store));
    this.api.getSubscription().subscribe((sub) => this.subscription.set(sub));
  }

  private populate(store: StoreDto): void {
    this.store.set(store);
    this.hours.set(store.settings.openingHours);
    this.profileForm.patchValue({
      name: store.name,
      nameAr: store.nameAr ?? '',
      slug: store.slug,
      whatsAppNumber: store.whatsAppNumber,
      instagramHandle: store.instagramHandle ?? '',
      description: store.description ?? '',
      descriptionAr: store.descriptionAr ?? '',
      locationText: store.locationText ?? '',
      governorate: store.governorate ?? '',
      isAcceptingOrders: store.isAcceptingOrders,
    });
    this.orderingForm.patchValue({
      deliveryFee: store.settings.deliveryFee,
      minimumOrderAmount: store.settings.minimumOrderAmount,
      deliveryEnabled: store.settings.deliveryEnabled,
      pickupEnabled: store.settings.pickupEnabled,
      defaultLanguage: store.settings.defaultLanguage,
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid || this.busy()) return;
    const store = this.store();
    if (!store) return;
    this.busy.set(true);
    this.error.set(null);
    const value = this.profileForm.getRawValue();

    this.api.updateStore({
      name: value.name,
      nameAr: value.nameAr || null,
      slug: value.slug.trim().toLowerCase(),
      whatsAppNumber: value.whatsAppNumber,
      instagramHandle: value.instagramHandle || null,
      description: value.description || null,
      descriptionAr: value.descriptionAr || null,
      locationText: value.locationText || null,
      governorate: value.governorate || null,
      wilayat: store.wilayat,
      isAcceptingOrders: value.isAcceptingOrders,
    }).subscribe({
      next: (updated) => this.onSaved(updated),
      error: (err) => this.onError(err),
    });
  }

  saveSettings(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const value = this.orderingForm.getRawValue();

    this.api.updateSettings({
      deliveryFee: Number(value.deliveryFee) || 0,
      minimumOrderAmount: Number(value.minimumOrderAmount) || 0,
      deliveryEnabled: value.deliveryEnabled,
      pickupEnabled: value.pickupEnabled,
      openingHours: this.hours(),
      defaultLanguage: value.defaultLanguage,
    }).subscribe({
      next: (updated) => this.onSaved(updated),
      error: (err) => this.onError(err),
    });
  }

  setClosed(day: number, closed: boolean): void {
    this.hours.update((hours) => hours.map((h) => (h.day === day ? { ...h, closed } : h)));
  }

  setTime(day: number, field: 'open' | 'close', event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    if (!value) return;
    this.hours.update((hours) => hours.map((h) => (h.day === day ? { ...h, [field]: value } : h)));
  }

  uploadLogo(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.api.uploadLogo(file).subscribe({
      next: (updated) => this.onSaved(updated),
      error: (err) => this.onError(err),
    });
  }

  removeLogo(): void {
    this.api.removeLogo().subscribe({
      next: (updated) => this.onSaved(updated),
      error: (err) => this.onError(err),
    });
  }

  uploadBanner(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.api.uploadBanner(file).subscribe({
      next: (updated) => this.onSaved(updated),
      error: (err) => this.onError(err),
    });
  }

  removeBanner(): void {
    this.api.removeBanner().subscribe({
      next: (updated) => this.onSaved(updated),
      error: (err) => this.onError(err),
    });
  }

  togglePlan(sub: SubscriptionDto): void {
    this.busy.set(true);
    this.api.changePlan(sub.plan === 'Pro' ? 'Free' : 'Pro').subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.subscription.set(updated);
        this.snackBar.open(this.i18n.t('settings.plan.changed'), undefined, { duration: 2000 });
      },
      error: (err) => this.onError(err),
    });
  }

  private onSaved(store: StoreDto): void {
    this.busy.set(false);
    this.populate(store);
    this.snackBar.open(this.i18n.t('common.saved'), undefined, { duration: 2000 });
  }

  private onError(err: unknown): void {
    this.busy.set(false);
    this.error.set(problemMessage(err, this.i18n.t('common.error')));
  }
}
