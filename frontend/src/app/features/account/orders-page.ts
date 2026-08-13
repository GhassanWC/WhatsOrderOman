import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountApiService } from '../../core/services/account-api.service';
import { BuyerOrderListItemDto } from '../../core/models/api-types';
import { EmptyState, StatusChip } from '../../shared/components';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';

const FILTERS = ['all', 'active', 'completed', 'cancelled'] as const;
type Filter = (typeof FILTERS)[number];

@Component({
  selector: 'wo-account-orders',
  imports: [RouterLink, DatePipe, TranslatePipe, BilingualPipe, OmrPricePipe, StatusChip, EmptyState],
  template: `
    <h1>{{ 'account.nav.orders' | t }}</h1>

    <div class="chips">
      @for (item of filters; track item) {
        <button class="chip" [class.on]="filter() === item" (click)="setFilter(item)">
          {{ 'account.orderFilter.' + item | t }}
        </button>
      }
    </div>

    @if (loading() && orders().length === 0) {
      <div class="wo-skeleton" style="height: 90px; margin-bottom: 10px"></div>
      <div class="wo-skeleton" style="height: 90px"></div>
    } @else if (orders().length === 0) {
      <wo-empty-state emoji="🧾" messageKey="account.noOrders">
        <a routerLink="/stores" class="wo-btn wo-btn--primary">{{ 'account.startShopping' | t }}</a>
      </wo-empty-state>
    } @else {
      <div class="list">
        @for (order of orders(); track order.id) {
          <div class="order wo-card">
            <a class="main" [routerLink]="['/account/orders', order.id]">
              <div class="logo">
                @if (order.storeLogoUrl; as logo) {
                  <img [src]="logo" alt="" loading="lazy" />
                } @else {
                  <span>{{ order.storeName.charAt(0) }}</span>
                }
              </div>
              <div class="mid">
                <div class="row1">
                  <b>{{ order.storeName | bilingual: order.storeNameAr }}</b>
                  <wo-status-chip [status]="order.status" />
                  @if (order.unreadMessages > 0) {
                    <span class="unread">{{ order.unreadMessages }}</span>
                  }
                </div>
                <small class="muted">#{{ order.orderNumber }} · {{ order.createdAt | date: 'medium' }}
                  · {{ 'account.fulfillment.' + order.fulfillmentMethod | t }}</small>
                <small class="items">{{ order.itemsSummary }}</small>
              </div>
              <b class="total">{{ order.total | omr }}</b>
            </a>
            <div class="actions">
              <a [routerLink]="['/account/orders', order.id]" class="wo-btn wo-btn--ghost mini">
                {{ 'account.viewOrder' | t }}
              </a>
              <a [routerLink]="['/account/orders', order.id]" [queryParams]="{ chat: 1 }"
                 class="wo-btn wo-btn--ghost mini">💬 {{ 'account.openChat' | t }}</a>
              <a [routerLink]="['/', order.storeSlug]" class="wo-btn wo-btn--ghost mini">
                {{ 'account.reorder' | t }}
              </a>
              @if (order.canReview) {
                <a [routerLink]="['/account/orders', order.id]" [queryParams]="{ review: 1 }"
                   class="wo-btn wo-btn--ghost mini review">★ {{ 'account.review' | t }}</a>
              }
            </div>
          </div>
        }
      </div>
      @if (orders().length < total()) {
        <div class="more">
          <button class="wo-btn wo-btn--ghost" (click)="loadMore()" [disabled]="loading()">
            {{ (loading() ? 'common.loading' : 'market.loadMore') | t }}
          </button>
        </div>
      }
    }
  `,
  styles: `
    h1 { margin: 0 0 14px; font-size: 22px; font-weight: 800; }
    .chips { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 14px; }
    .chip { border: 1px solid var(--wo-border); background: var(--wo-surface); cursor: pointer;
      padding: 7px 14px; border-radius: 999px; font: 600 13.5px var(--wo-font); color: var(--wo-ink-soft); }
    .chip.on { background: var(--wo-primary-soft); border-color: var(--wo-primary); color: var(--wo-primary-dark); }
    .list { display: grid; gap: 10px; }
    .order { padding: 14px 16px; }
    .main { display: flex; gap: 12px; align-items: center; text-decoration: none; color: inherit; }
    .logo { width: 46px; height: 46px; border-radius: 12px; overflow: hidden; flex-shrink: 0;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      display: grid; place-items: center; font-weight: 800; font-size: 18px; }
    .logo img { width: 100%; height: 100%; object-fit: cover; }
    .mid { flex: 1; min-width: 0; display: grid; gap: 2px; }
    .row1 { display: flex; align-items: center; gap: 8px; flex-wrap: wrap;
      b { font-size: 15px; } }
    .unread { background: var(--wo-danger); color: #fff; border-radius: 999px; min-width: 18px;
      height: 18px; padding-inline: 5px; font-size: 11px; font-weight: 700;
      display: inline-grid; place-items: center; }
    .items { color: var(--wo-ink-soft); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .total { white-space: nowrap; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 10px; }
    .mini { padding: 6px 12px; font-size: 12.5px; }
    .review { color: #b45309; }
    .more { text-align: center; margin-top: 16px; }
  `,
})
export class AccountOrdersPage {
  private readonly api = inject(AccountApiService);

  protected readonly filters = FILTERS;
  readonly filter = signal<Filter>('all');
  readonly loading = signal(true);
  readonly orders = signal<BuyerOrderListItemDto[]>([]);
  readonly total = signal(0);
  private page = 1;

  constructor() {
    this.fetch(true);
  }

  setFilter(filter: Filter): void {
    this.filter.set(filter);
    this.page = 1;
    this.fetch(true);
  }

  loadMore(): void {
    this.page += 1;
    this.fetch(false);
  }

  private fetch(replace: boolean): void {
    this.loading.set(true);
    this.api.orders(this.filter(), this.page, 10).subscribe({
      next: (result) => {
        this.orders.update((current) => (replace ? result.items : [...current, ...result.items]));
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
