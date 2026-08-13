import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { StoreApiService } from '../../core/services/store-api.service';
import { AuthService } from '../../core/services/auth.service';
import { problemMessage, LangSwitcher } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

const GOVERNORATES = [
  'muscat', 'dhofar', 'musandam', 'buraimi', 'dakhiliyah', 'batinahNorth',
  'batinahSouth', 'sharqiyahNorth', 'sharqiyahSouth', 'dhahirah', 'wusta',
] as const;

@Component({
  selector: 'wo-create-store-page',
  imports: [ReactiveFormsModule, LangSwitcher, TranslatePipe],
  template: `
    <div class="page">
      <header class="bar container">
        <span class="logo">Whats<span class="accent">Order</span></span>
        <wo-lang-switcher />
      </header>

      <main class="container">
        <div class="card wo-card">
          <h1>{{ 'onboarding.title' | t }}</h1>
          <p class="muted subtitle">{{ 'onboarding.subtitle' | t }}</p>

          <form [formGroup]="form" (ngSubmit)="submit()" class="form">
            <div class="row">
              <div class="wo-field">
                <label for="name">{{ 'onboarding.storeName' | t }}</label>
                <input id="name" formControlName="name" (input)="suggestSlug()" />
              </div>
              <div class="wo-field">
                <label for="nameAr">{{ 'onboarding.storeNameAr' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
                <input id="nameAr" formControlName="nameAr" dir="rtl" />
              </div>
            </div>

            <div class="wo-field">
              <label for="slug">{{ 'onboarding.slug' | t }}</label>
              <div class="slug-input">
                <span class="slug-prefix">whatsorder.om/</span>
                <input id="slug" formControlName="slug" (input)="slugEdited = true"
                       autocapitalize="off" autocomplete="off" spellcheck="false" />
              </div>
              @switch (slugState()) {
                @case ('checking') { <span class="hint">{{ 'onboarding.slugChecking' | t }}</span> }
                @case ('available') { <span class="ok">✓ {{ 'onboarding.slugAvailable' | t }}</span> }
                @case ('taken') { <span class="error">{{ 'onboarding.slugTaken' | t }}</span> }
                @case ('invalid') { <span class="error">{{ 'onboarding.slugInvalid' | t }}</span> }
                @default { <span class="hint">{{ 'onboarding.slugHint' | t }}</span> }
              }
            </div>

            <div class="row">
              <div class="wo-field">
                <label for="wa">{{ 'onboarding.whatsapp' | t }} <small class="muted">({{ 'common.optional' | t }})</small></label>
                <input id="wa" formControlName="whatsAppNumber" inputmode="tel" placeholder="9123 4567" />
                <span class="hint">{{ 'onboarding.whatsappHint' | t }}</span>
              </div>
              <div class="wo-field">
                <label for="ig">{{ 'onboarding.instagram' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
                <input id="ig" formControlName="instagramHandle" placeholder="my.store" />
              </div>
            </div>

            <div class="row">
              <div class="wo-field">
                <label for="gov">{{ 'onboarding.governorate' | t }}</label>
                <select id="gov" formControlName="governorate">
                  <option value=""></option>
                  @for (gov of governorates; track gov) {
                    <option [value]="gov">{{ 'onboarding.governorates.' + gov | t }}</option>
                  }
                </select>
              </div>
              <div class="wo-field">
                <label for="loc">{{ 'onboarding.location' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
                <input id="loc" formControlName="locationText" />
              </div>
            </div>

            <div class="wo-field">
              <label for="desc">{{ 'onboarding.description' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
              <textarea id="desc" formControlName="description" rows="2"></textarea>
            </div>

            @if (error()) {
              <p class="error-banner">{{ error() }}</p>
            }

            <button class="wo-btn wo-btn--primary submit" type="submit"
                    [disabled]="form.invalid || slugState() !== 'available' || busy()">
              {{ 'onboarding.create' | t }} 🚀
            </button>
          </form>
        </div>
      </main>
    </div>
  `,
  styles: `
    .page { min-height: 100dvh; padding-bottom: 64px; }
    .bar { display: flex; justify-content: space-between; align-items: center; padding-block: 18px; }
    .logo { font: 800 22px var(--wo-font); .accent { color: var(--wo-primary); } }
    .card { max-width: 620px; margin: 3vh auto 0; padding: 32px 28px;
      h1 { font-size: 24px; } .subtitle { margin: 4px 0 24px; } }
    .form { display: grid; gap: 18px; }
    .row { display: grid; gap: 18px; grid-template-columns: 1fr 1fr;
      @media (max-width: 560px) { grid-template-columns: 1fr; } }
    .slug-input { display: flex; align-items: center; border: 1.5px solid var(--wo-border);
      border-radius: var(--wo-radius-sm); background: var(--wo-surface); overflow: hidden;
      direction: ltr;
      &:focus-within { border-color: var(--wo-primary); box-shadow: 0 0 0 3px var(--wo-primary-soft); }
      .slug-prefix { padding-inline: 12px 2px; color: var(--wo-muted); font-weight: 600; font-size: 14px; }
      input { border: none !important; box-shadow: none !important; padding-inline-start: 2px; } }
    .ok { color: #15803d; font-size: 12.5px; font-weight: 600; }
    .error { color: var(--wo-danger); font-size: 12.5px; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
    .submit { justify-self: start; }
  `,
})
export class CreateStorePage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(StoreApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslationService);

  readonly governorates = GOVERNORATES;
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly slugState = signal<'idle' | 'checking' | 'available' | 'taken' | 'invalid'>('idle');

  protected slugEdited = false;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    nameAr: [''],
    slug: ['', [Validators.required]],
    // Optional — the built-in order chat works without WhatsApp.
    whatsAppNumber: ['', Validators.pattern(/^[+\d][\d\s\-()]{6,18}$/)],
    instagramHandle: [''],
    governorate: [''],
    locationText: [''],
    description: ['', Validators.maxLength(500)],
  });

  constructor() {
    this.form.controls.slug.valueChanges
      .pipe(
        debounceTime(350),
        distinctUntilChanged(),
        switchMap((slug) => {
          const normalized = slug.trim().toLowerCase();
          if (!/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/.test(normalized) || normalized.length < 3) {
            this.slugState.set(normalized ? 'invalid' : 'idle');
            return [];
          }
          this.slugState.set('checking');
          return this.api.checkSlug(normalized);
        }),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.slugState.set(!result.isValid ? 'invalid' : result.isAvailable ? 'available' : 'taken');
      });
  }

  /** Auto-suggest a slug from the store name until the owner edits the slug manually. */
  suggestSlug(): void {
    if (this.slugEdited && this.form.controls.slug.value) return;
    const suggested = this.form.controls.name.value
      .toLowerCase()
      .normalize('NFKD')
      .replace(/[^a-z0-9\s-]/g, '')
      .trim()
      .replace(/[\s]+/g, '-')
      .replace(/-{2,}/g, '-')
      .slice(0, 40);
    this.form.controls.slug.setValue(suggested);
  }

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.slugEdited = true;
    this.busy.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();

    this.api.createStore({
      name: value.name,
      nameAr: value.nameAr || null,
      slug: value.slug.trim().toLowerCase(),
      whatsAppNumber: value.whatsAppNumber,
      instagramHandle: value.instagramHandle || null,
      description: value.description || null,
      descriptionAr: null,
      locationText: value.locationText || null,
      governorate: value.governorate ? this.i18n.t('onboarding.governorates.' + value.governorate) : null,
      wilayat: null,
    }).subscribe({
      next: () => {
        // Rotate tokens (not just the profile): a buyer who just opened a store
        // was granted the Owner role, which must land in the access token before
        // the dashboard's role-guarded endpoints are called.
        this.auth.refresh().subscribe({
          next: () => void this.router.navigate(['/dashboard']),
          error: () => void this.router.navigate(['/dashboard']),
        });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err, this.i18n.t('common.error')));
      },
    });
  }
}
