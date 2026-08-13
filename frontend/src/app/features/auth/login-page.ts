import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { problemMessage } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';
import { AuthShell } from './auth-shell';

@Component({
  selector: 'wo-login-page',
  imports: [ReactiveFormsModule, RouterLink, AuthShell, TranslatePipe],
  template: `
    <wo-auth-shell titleKey="auth.loginTitle" subtitleKey="auth.loginSubtitle">
      <form [formGroup]="form" (ngSubmit)="submit()" class="form">
        <div class="wo-field">
          <label for="email">{{ 'auth.email' | t }}</label>
          <input id="email" type="email" formControlName="email" autocomplete="email" />
        </div>
        <div class="wo-field">
          <label for="password">{{ 'auth.password' | t }}</label>
          <input id="password" type="password" formControlName="password" autocomplete="current-password" />
          <a routerLink="/forgot-password" class="forgot">{{ 'auth.forgotPassword' | t }}</a>
        </div>

        @if (error()) {
          <p class="error-banner">{{ error() }}</p>
        }

        <button class="wo-btn wo-btn--primary" type="submit" [disabled]="form.invalid || busy()">
          {{ 'auth.login' | t }}
        </button>
      </form>
      <p class="switch">
        {{ 'auth.noAccount' | t }} <a routerLink="/register">{{ 'auth.register' | t }}</a>
      </p>
    </wo-auth-shell>
  `,
  styles: `
    .form { display: grid; gap: 16px; }
    .forgot { font-size: 13px; justify-self: end; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
    .switch { margin-top: 20px; text-align: center; font-size: 14px; color: var(--wo-ink-soft); }
  `,
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly i18n = inject(TranslationService);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: (auth) => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const isSeller = auth.user.roles?.includes('Owner') ?? true;
        const sellerTarget = auth.user.hasStore ? '/dashboard' : '/onboarding';
        const target = returnUrl ?? (isSeller ? sellerTarget : '/');
        void this.router.navigateByUrl(target);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err, this.i18n.t('auth.invalidCredentials')));
      },
    });
  }
}
