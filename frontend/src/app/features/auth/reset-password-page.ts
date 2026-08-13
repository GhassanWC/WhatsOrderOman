import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { problemMessage } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';
import { AuthShell } from './auth-shell';

@Component({
  selector: 'wo-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink, AuthShell, TranslatePipe],
  template: `
    <wo-auth-shell titleKey="auth.resetTitle" subtitleKey="auth.passwordHint">
      @if (done()) {
        <p class="success">{{ 'auth.resetDone' | t }}</p>
        <a routerLink="/login" class="wo-btn wo-btn--primary">{{ 'auth.login' | t }}</a>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="form">
          <div class="wo-field">
            <label for="password">{{ 'auth.newPassword' | t }}</label>
            <input id="password" type="password" formControlName="password" autocomplete="new-password" />
            <span class="hint">{{ 'auth.passwordHint' | t }}</span>
          </div>

          @if (error()) {
            <p class="error-banner">{{ error() }}</p>
          }

          <button class="wo-btn wo-btn--primary" type="submit" [disabled]="form.invalid || busy()">
            {{ 'auth.resetPassword' | t }}
          </button>
        </form>
      }
    </wo-auth-shell>
  `,
  styles: `
    .form { display: grid; gap: 16px; }
    .success { background: var(--wo-primary-soft); color: var(--wo-primary-dark); border-radius: 10px;
      padding: 14px; font-size: 14px; margin-bottom: 16px; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
  `,
})
export class ResetPasswordPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly i18n = inject(TranslationService);

  readonly busy = signal(false);
  readonly done = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    password: ['', [
      Validators.required,
      Validators.minLength(8),
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/),
    ]],
  });

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    const email = this.route.snapshot.queryParamMap.get('email') ?? '';
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';

    this.busy.set(true);
    this.error.set(null);
    this.auth.resetPassword(email, token, this.form.getRawValue().password).subscribe({
      next: () => this.done.set(true),
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err, this.i18n.t('common.error')));
      },
    });
  }
}
