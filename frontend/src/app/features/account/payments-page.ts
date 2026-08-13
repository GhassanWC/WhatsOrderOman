import { Component } from '@angular/core';
import { TranslatePipe } from '../../shared/pipes';

/**
 * Placeholder for future tokenized payment methods. No card data is ever stored
 * here — a payment provider integration will manage methods on its own vault.
 */
@Component({
  selector: 'wo-account-payments',
  imports: [TranslatePipe],
  template: `
    <h1>{{ 'account.nav.payments' | t }}</h1>
    <div class="wo-card coming">
      <span class="emoji">💳</span>
      <h2>{{ 'account.paymentsComingTitle' | t }}</h2>
      <p class="muted">{{ 'account.paymentsComingText' | t }}</p>
      <p class="badge">🇴🇲 {{ 'account.cashNote' | t }}</p>
    </div>
  `,
  styles: `
    h1 { margin: 0 0 14px; font-size: 22px; font-weight: 800; }
    .coming { display: grid; gap: 8px; justify-items: center; text-align: center; padding: 44px 24px;
      .emoji { font-size: 44px; }
      h2 { margin: 0; font-size: 18px; }
      p { margin: 0; max-width: 460px; } }
    .badge { background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      border-radius: 999px; padding: 7px 16px; font-size: 13.5px; font-weight: 600; margin-top: 6px; }
  `,
})
export class AccountPaymentsPage {}
