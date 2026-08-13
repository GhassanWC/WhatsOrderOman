import { Component, inject, signal } from '@angular/core';
import { MarketplaceApiService } from '../../core/services/marketplace-api.service';
import { PublicOfferDto } from '../../core/models/api-types';
import { EmptyState } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { CardSkeletons, OfferCard } from './cards';

@Component({
  selector: 'wo-offers-page',
  imports: [TranslatePipe, OfferCard, CardSkeletons, EmptyState],
  template: `
    <div class="container page">
      <h1>{{ 'market.offersTitle' | t }}</h1>
      <p class="sub">{{ 'market.offersSubtitle' | t }}</p>

      @if (loading()) {
        <div class="grid"><wo-card-skeletons [count]="6" /></div>
      } @else if (offers().length === 0) {
        <wo-empty-state emoji="🏷️" messageKey="market.noOffers" />
      } @else {
        <div class="grid">
          @for (offer of offers(); track offer.id) {
            <wo-offer-card [offer]="offer" />
          }
        </div>
      }
    </div>
  `,
  styles: `
    .page { padding-block: 24px 46px; }
    h1 { margin: 0; font-size: 24px; font-weight: 800; }
    .sub { margin: 6px 0 18px; color: var(--wo-ink-soft); }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 12px; }
  `,
})
export class OffersPage {
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
