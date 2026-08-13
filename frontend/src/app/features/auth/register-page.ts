import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { problemMessage } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';
import { AuthShell } from './auth-shell';

@Component({
  selector: 'wo-register-page',
  imports: [ReactiveFormsModule, RouterLink, AuthShell, TranslatePipe],
  template: `
    <wo-auth-shell titleKey="auth.registerTitle" subtitleKey="auth.registerSubtitle">
      <form [formGroup]="form" (ngSubmit)="submit()" class="form">
        <div class="type-toggle" role="radiogroup" [attr.aria-label]="'auth.accountType' | t">
          <button type="button" class="type-btn" [class.active]="accountType() === 'buyer'"
                  (click)="accountType.set('buyer')">
            <span class="type-emoji">🛍️</span>
            <span>{{ 'auth.typeBuyer' | t }}</span>
            <small>{{ 'auth.typeBuyerHint' | t }}</small>
          </button>
          <button type="button" class="type-btn" [class.active]="accountType() === 'seller'"
                  (click)="accountType.set('seller')">
            <span class="type-emoji">🏪</span>
            <span>{{ 'auth.typeSeller' | t }}</span>
            <small>{{ 'auth.typeSellerHint' | t }}</small>
          </button>
        </div>
        <div class="wo-field">
          <label for="name">{{ 'auth.displayName' | t }}</label>
          <input id="name" type="text" formControlName="displayName" autocomplete="name" />
        </div>
        <div class="wo-field">
          <label for="email">{{ 'auth.email' | t }}</label>
          <input id="email" type="email" formControlName="email" autocomplete="email" />
        </div>
        <div class="wo-field">
          <label for="password">{{ 'auth.password' | t }}</label>
          <input id="password" type="password" formControlName="password" autocomplete="new-password" />
          <span class="hint">{{ 'auth.passwordHint' | t }}</span>
        </div>

        @if (error()) {
          <p class="error-banner">{{ error() }}</p>
        }

        <button class="wo-btn wo-btn--primary" type="submit" [disabled]="form.invalid || busy()">
          {{ 'auth.register' | t }}
        </button>
      </form>
      <p class="switch">
        {{ 'auth.haveAccount' | t }} <a routerLink="/login">{{ 'auth.login' | t }}</a>
      </p>
    </wo-auth-shell>
  `,
  styles: `
    .form { display: grid; gap: 16px; }
    .type-toggle { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .type-btn {
      display: grid; gap: 2px; justify-items: center; text-align: center;
      padding: 12px 8px; border-radius: var(--wo-radius-sm); cursor: pointer;
      border: 1.5px solid var(--wo-border); background: var(--wo-surface);
      font: inherit; color: var(--wo-ink); transition: border-color .15s, background .15s;
    }
    .type-btn small { color: var(--wo-muted); font-size: 11.5px; }
    .type-btn.active { border-color: var(--wo-primary); background: var(--wo-primary-soft); }
    .type-emoji { font-size: 20px; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
    .switch { margin-top: 20px; text-align: center; font-size: 14px; color: var(--wo-ink-soft); }
  `,
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly i18n = inject(TranslationService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly accountType = signal<'buyer' | 'seller'>(
    this.route.snapshot.queryParamMap.get('as') === 'seller' ? 'seller' : 'buyer',
  );

  readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [
      Validators.required,
      Validators.minLength(8),
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/),
    ]],
  });

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const { displayName, email, password } = this.form.getRawValue();

    this.auth.register(email, password, displayName, this.accountType()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const target =
          this.accountType() === 'seller' ? '/onboarding' : (returnUrl ?? '/');
        void this.router.navigateByUrl(target);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err, this.i18n.t('common.error')));
      },
    });
  }
}
