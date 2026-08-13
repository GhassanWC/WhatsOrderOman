import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountApiService } from '../../core/services/account-api.service';
import { AuthService } from '../../core/services/auth.service';
import { BuyerProfileDto } from '../../core/models/api-types';
import { problemMessage } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'wo-account-profile',
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <h1>{{ 'account.nav.profile' | t }}</h1>

    @if (profile(); as p) {
      <div class="wo-card panel avatar-panel">
        <div class="avatar">
          @if (p.avatarUrl; as avatar) {
            <img [src]="avatar" alt="" />
          } @else {
            <span>{{ p.displayName.charAt(0) }}</span>
          }
        </div>
        <div>
          <b>{{ p.displayName }}</b>
          <small class="muted block">{{ p.email }}</small>
          <label class="wo-btn wo-btn--ghost mini upload">
            {{ 'account.changePhoto' | t }}
            <input type="file" accept="image/*" hidden (change)="uploadAvatar($event)" />
          </label>
        </div>
      </div>

      <form class="wo-card panel" [formGroup]="form" (ngSubmit)="save()">
        <div class="wo-field">
          <label>{{ 'auth.displayName' | t }}</label>
          <input formControlName="displayName" autocomplete="name" />
        </div>
        <div class="wo-field">
          <label>{{ 'checkout.phone' | t }}</label>
          <input formControlName="phone" inputmode="tel" dir="ltr" placeholder="9123 4567" />
        </div>
        <div class="wo-field">
          <label>{{ 'auth.email' | t }}</label>
          <input [value]="p.email" disabled />
          <span class="hint">{{ 'account.emailLocked' | t }}</span>
        </div>
        <div class="wo-field">
          <label>{{ 'account.preferredLanguage' | t }}</label>
          <select formControlName="preferredLanguage">
            <option value="">{{ 'account.langAuto' | t }}</option>
            <option value="en">English</option>
            <option value="ar">العربية</option>
          </select>
        </div>

        <h2>{{ 'account.notificationPrefs' | t }}</h2>
        <label class="check">
          <input type="checkbox" formControlName="notifyOrderUpdates" />
          {{ 'account.notifyOrders' | t }}
        </label>
        <label class="check">
          <input type="checkbox" formControlName="notifyMessages" />
          {{ 'account.notifyMessages' | t }}
        </label>
        <label class="check">
          <input type="checkbox" formControlName="notifyOffers" />
          {{ 'account.notifyOffers' | t }}
        </label>

        @if (error()) { <p class="error-banner">{{ error() }}</p> }

        <button type="submit" class="wo-btn wo-btn--primary" [disabled]="form.invalid || busy()">
          {{ 'common.save' | t }}
        </button>
      </form>
    } @else {
      <div class="wo-skeleton" style="height: 260px"></div>
    }
  `,
  styles: `
    h1 { margin: 0 0 14px; font-size: 22px; font-weight: 800; }
    .panel { padding: 18px; display: grid; gap: 12px; margin-bottom: 14px;
      h2 { margin: 6px 0 0; font-size: 15px; } }
    .avatar-panel { grid-template-columns: auto 1fr; align-items: center; display: grid; }
    .avatar { width: 72px; height: 72px; border-radius: 50%; overflow: hidden;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      display: grid; place-items: center; font-weight: 800; font-size: 28px; }
    .avatar img { width: 100%; height: 100%; object-fit: cover; }
    .block { display: block; }
    .upload { margin-top: 8px; display: inline-block; cursor: pointer; padding: 6px 12px; font-size: 12.5px; }
    select { width: 100%; border: 1.5px solid var(--wo-border); border-radius: var(--wo-radius-sm);
      background: var(--wo-surface); padding: 10px 12px; font: inherit; outline: none; }
    .check { display: flex; align-items: center; gap: 8px; font-size: 14px; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
  `,
})
export class AccountProfilePage {
  private readonly api = inject(AccountApiService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);

  readonly profile = signal<BuyerProfileDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    phone: [''],
    preferredLanguage: [''],
    notifyOrderUpdates: [true],
    notifyMessages: [true],
    notifyOffers: [true],
  });

  constructor() {
    this.api.profile().subscribe({
      next: (profile) => this.apply(profile),
      error: () => undefined,
    });
  }

  private apply(profile: BuyerProfileDto): void {
    this.profile.set(profile);
    this.form.patchValue({
      displayName: profile.displayName,
      phone: profile.phone ?? '',
      preferredLanguage: profile.preferredLanguage ?? '',
      notifyOrderUpdates: profile.notifyOrderUpdates,
      notifyMessages: profile.notifyMessages,
      notifyOffers: profile.notifyOffers,
    });
  }

  save(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    this.api.updateProfile({
      displayName: value.displayName.trim(),
      phone: value.phone.trim() || null,
      preferredLanguage: value.preferredLanguage || null,
      notifyOrderUpdates: value.notifyOrderUpdates,
      notifyMessages: value.notifyMessages,
      notifyOffers: value.notifyOffers,
    }).subscribe({
      next: (profile) => {
        this.busy.set(false);
        this.apply(profile);
        this.auth.refreshMe().subscribe({ error: () => undefined });
        if (profile.preferredLanguage === 'en' || profile.preferredLanguage === 'ar') {
          void this.i18n.setLang(profile.preferredLanguage);
        }
        this.snackBar.open(this.i18n.t('common.saved'), undefined, { duration: 2000 });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err, this.i18n.t('common.error')));
      },
    });
  }

  uploadAvatar(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.api.uploadAvatar(file).subscribe({
      next: (profile) => {
        this.apply(profile);
        this.auth.refreshMe().subscribe({ error: () => undefined });
      },
      error: (err) =>
        this.snackBar.open(problemMessage(err, this.i18n.t('common.error')), undefined, { duration: 3000 }),
    });
  }
}
