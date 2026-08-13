import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TranslatePipe } from '../../shared/pipes';
import { AuthShell } from './auth-shell';

@Component({
  selector: 'wo-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink, AuthShell, TranslatePipe],
  template: `
    <wo-auth-shell titleKey="auth.forgotTitle" subtitleKey="auth.forgotSubtitle">
      @if (sent()) {
        <p class="success">{{ 'auth.resetSent' | t }}</p>
        <a routerLink="/login" class="wo-btn wo-btn--ghost">{{ 'auth.login' | t }}</a>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="form">
          <div class="wo-field">
            <label for="email">{{ 'auth.email' | t }}</label>
            <input id="email" type="email" formControlName="email" autocomplete="email" />
          </div>
          <button class="wo-btn wo-btn--primary" type="submit" [disabled]="form.invalid || busy()">
            {{ 'auth.sendResetLink' | t }}
          </button>
        </form>
      }
    </wo-auth-shell>
  `,
  styles: `
    .form { display: grid; gap: 16px; }
    .success { background: var(--wo-primary-soft); color: var(--wo-primary-dark); border-radius: 10px;
      padding: 14px; font-size: 14px; margin-bottom: 16px; }
  `,
})
export class ForgotPasswordPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  readonly busy = signal(false);
  readonly sent = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.auth.forgotPassword(this.form.getRawValue().email).subscribe({
      next: () => this.sent.set(true),
      error: () => this.sent.set(true), // same response either way — no account enumeration
    });
  }
}
