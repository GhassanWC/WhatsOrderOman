import { Component } from '@angular/core';
import { TranslatePipe } from '../../shared/pipes';

/** Placeholder for future invoices, receipts and refunds history. */
@Component({
  selector: 'wo-account-billing',
  imports: [TranslatePipe],
  template: `
    <h1>{{ 'account.nav.billing' | t }}</h1>
    <div class="wo-card coming">
      <span class="emoji">📄</span>
      <h2>{{ 'account.billingComingTitle' | t }}</h2>
      <p class="muted">{{ 'account.billingComingText' | t }}</p>
    </div>
  `,
  styles: `
    h1 { margin: 0 0 14px; font-size: 22px; font-weight: 800; }
    .coming { display: grid; gap: 8px; justify-items: center; text-align: center; padding: 44px 24px;
      .emoji { font-size: 44px; }
      h2 { margin: 0; font-size: 18px; }
      p { margin: 0; max-width: 460px; } }
  `,
})
export class AccountBillingPage {}
