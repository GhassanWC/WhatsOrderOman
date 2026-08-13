import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { StoreApiService } from '../../core/services/store-api.service';
import { StoreReviewsPage } from '../../core/models/api-types';
import { EmptyState } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';

/** What buyers say about the store — read-only for the owner. */
@Component({
  selector: 'wo-seller-reviews-page',
  imports: [DatePipe, TranslatePipe, EmptyState],
  template: `
    <h1>{{ 'reviews.title' | t }}</h1>

    @if (page(); as p) {
      @if (p.reviewsCount > 0) {
        <div class="summary wo-card">
          <b class="big">{{ p.averageRating }}</b>
          <div>
            <span class="stars">★★★★★</span>
            <small class="muted block">{{ 'reviews.count' | t: { count: p.reviewsCount } }}</small>
          </div>
        </div>
        <div class="list">
          @for (review of p.items; track review.id) {
            <div class="review wo-card">
              <div class="head">
                <b>{{ review.reviewerName }}</b>
                <span class="stars small">{{ '★★★★★'.slice(0, review.rating) }}</span>
                <small class="muted">{{ review.createdAt | date: 'mediumDate' }}</small>
              </div>
              @if (review.comment) { <p>{{ review.comment }}</p> }
            </div>
          }
        </div>
        @if (p.items.length < p.total) {
          <div class="more">
            <button class="wo-btn wo-btn--ghost" (click)="loadMore()">{{ 'market.loadMore' | t }}</button>
          </div>
        }
      } @else {
        <wo-empty-state emoji="⭐" messageKey="reviews.empty" />
      }
    } @else {
      <div class="wo-skeleton" style="height: 90px; margin-bottom: 10px"></div>
      <div class="wo-skeleton" style="height: 90px"></div>
    }
  `,
  styles: `
    h1 { margin: 0 0 16px; font-size: 22px; }
    .summary { display: flex; align-items: center; gap: 14px; padding: 16px 20px; margin-bottom: 14px;
      .big { font-size: 34px; font-weight: 800; } }
    .stars { color: #f59e0b; letter-spacing: 2px; }
    .stars.small { font-size: 13px; }
    .block { display: block; }
    .list { display: grid; gap: 10px; }
    .review { padding: 14px 16px;
      p { margin: 8px 0 0; font-size: 14px; color: var(--wo-ink-soft); } }
    .head { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap;
      b { font-size: 14.5px; } }
    .more { text-align: center; margin-top: 14px; }
  `,
})
export class SellerReviewsPage {
  private readonly api = inject(StoreApiService);

  readonly page = signal<StoreReviewsPage | null>(null);
  private currentPage = 1;

  constructor() {
    this.load();
  }

  loadMore(): void {
    this.currentPage += 1;
    this.load(true);
  }

  private load(append = false): void {
    this.api.getStoreReviews(this.currentPage, 10).subscribe({
      next: (result) => {
        this.page.update((existing) =>
          append && existing
            ? { ...result, items: [...existing.items, ...result.items] }
            : result);
      },
      error: () => undefined,
    });
  }
}
