import { Component, inject, signal } from '@angular/core';
import { MarketplaceApiService } from '../../core/services/marketplace-api.service';
import { PublicOfferDto } from '../../core/models/api-types';
import { EmptyState } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { CardSkeletons, OfferCard } from '../marketplace/cards';

/** Offers available to the buyer right now (marketplace-wide, soonest ending first). */
@Component({
  selector: 'wo-account-offers',
  imports: [TranslatePipe, OfferCard, CardSkeletons, EmptyState],
  template: `
    <h1>{{ 'account.nav.offers' | t }}</h1>
    <p class="sub muted">{{ 'account.offersSubtitle' | t }}</p>

    @if (loading()) {
      <div class="grid"><wo-card-skeletons [count]="4" /></div>
    } @else if (offers().length === 0) {
      <wo-empty-state emoji="🏷️" messageKey="market.noOffers" />
    } @else {
      <div class="grid">
        @for (offer of offers(); track offer.id) {
          <wo-offer-card [offer]="offer" />
        }
      </div>
    }
  `,
  styles: `
    h1 { margin: 0 0 4px; font-size: 22px; font-weight: 800; }
    .sub { margin: 0 0 16px; font-size: 14px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(270px, 1fr)); gap: 12px; }
  `,
})
export class AccountOffersPage {
  private readonly api = inject(MarketplaceApiService);

  readonly loading = signal(true);
  readonly offers = signal<PublicOfferDto[]>([]);

  constructor() {
    this.api.offers(24).subscribe({
      next: (offers) => {
        this.offers.set(offers);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
