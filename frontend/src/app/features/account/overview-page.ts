import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountApiService } from '../../core/services/account-api.service';
import { AccountOverviewDto } from '../../core/models/api-types';
import { StatusChip } from '../../shared/components';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';

/** Account overview: continue shopping or track what's in flight — at a glance. */
@Component({
  selector: 'wo-account-overview',
  imports: [RouterLink, DatePipe, TranslatePipe, BilingualPipe, OmrPricePipe, StatusChip],
  template: `
    @if (overview(); as o) {
      <h1>{{ 'account.greeting' | t: { name: o.profile.displayName } }}</h1>

      <div class="stats">
        <a routerLink="/account/orders" class="stat wo-card">
          <b>{{ o.activeOrdersCount }}</b><span>{{ 'account.stat.activeOrders' | t }}</span>
        </a>
        <a routerLink="/account/messages" class="stat wo-card" [class.hot]="o.unreadMessages > 0">
          <b>{{ o.unreadMessages }}</b><span>{{ 'account.stat.unreadMessages' | t }}</span>
        </a>
        <a routerLink="/account/favorites" class="stat wo-card">
          <b>{{ o.favoriteStoresCount + o.favoriteProductsCount }}</b><span>{{ 'account.stat.favorites' | t }}</span>
        </a>
        <a routerLink="/account/notifications" class="stat wo-card" [class.hot]="o.unreadNotifications > 0">
          <b>{{ o.unreadNotifications }}</b><span>{{ 'account.stat.notifications' | t }}</span>
        </a>
      </div>

      @if (o.recentOrders.length > 0) {
        <section class="panel wo-card">
          <header>
            <h2>{{ 'account.recentOrders' | t }}</h2>
            <a routerLink="/account/orders">{{ 'market.seeAll' | t }}</a>
          </header>
          @for (order of o.recentOrders; track order.id) {
            <a class="order-row" [routerLink]="['/account/orders', order.id]">
              <div class="logo">
                @if (order.storeLogoUrl; as logo) {
                  <img [src]="logo" alt="" />
                } @else {
                  <span>{{ order.storeName.charAt(0) }}</span>
                }
              </div>
              <div class="mid">
                <b>{{ order.storeName | bilingual: order.storeNameAr }}</b>
                <small>#{{ order.orderNumber }} · {{ order.createdAt | date: 'mediumDate' }}</small>
              </div>
              <div class="end">
                <wo-status-chip [status]="order.status" />
                <b>{{ order.total | omr }}</b>
              </div>
            </a>
          }
        </section>
      } @else {
        <section class="panel wo-card empty">
          <span class="emoji">🛍️</span>
          <p>{{ 'account.noOrdersYet' | t }}</p>
          <a routerLink="/stores" class="wo-btn wo-btn--primary">{{ 'account.startShopping' | t }}</a>
        </section>
      }

      @if (o.defaultAddress; as address) {
        <section class="panel wo-card">
          <header>
            <h2>{{ 'account.defaultAddress' | t }}</h2>
            <a routerLink="/account/addresses">{{ 'common.edit' | t }}</a>
          </header>
          <p class="addr">📍 <b>{{ address.label }}</b> — {{ address.recipientName }},
            {{ [address.area, address.city, address.wilayat, address.governorate].join(' ') }}</p>
        </section>
      }
    } @else {
      <div class="wo-skeleton" style="height: 90px; margin-bottom: 14px"></div>
      <div class="wo-skeleton" style="height: 220px"></div>
    }
  `,
  styles: `
    h1 { margin: 0 0 16px; font-size: 22px; font-weight: 800; }
    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 10px; margin-bottom: 16px; }
    .stat { display: grid; gap: 2px; padding: 14px 16px; text-decoration: none; color: inherit;
      b { font-size: 22px; }
      span { font-size: 12.5px; color: var(--wo-ink-soft); } }
    .stat.hot b { color: var(--wo-danger); }
    .panel { padding: 16px 18px; margin-bottom: 14px;
      header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 10px;
        h2 { margin: 0; font-size: 16px; }
        a { font-size: 13px; color: var(--wo-primary-dark); font-weight: 600; text-decoration: none; } } }
    .order-row { display: flex; align-items: center; gap: 12px; padding: 10px 0; text-decoration: none;
      color: inherit; border-top: 1px solid var(--wo-border); }
    .order-row:first-of-type { border-top: none; }
    .logo { width: 40px; height: 40px; border-radius: 10px; overflow: hidden; flex-shrink: 0;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      display: grid; place-items: center; font-weight: 800; }
    .logo img { width: 100%; height: 100%; object-fit: cover; }
    .mid { flex: 1; min-width: 0; display: grid;
      b { font-size: 14px; }
      small { color: var(--wo-muted); font-size: 12px; } }
    .end { display: grid; gap: 4px; justify-items: end;
      b { font-size: 14px; } }
    .empty { text-align: center; display: grid; gap: 10px; justify-items: center; padding: 30px;
      .emoji { font-size: 38px; }
      p { margin: 0; color: var(--wo-ink-soft); } }
    .addr { margin: 0; font-size: 14px; color: var(--wo-ink-soft); }
  `,
})
export class AccountOverviewPage {
  private readonly api = inject(AccountApiService);
  readonly overview = signal<AccountOverviewDto | null>(null);

  constructor() {
    this.api.overview().subscribe({
      next: (overview) => this.overview.set(overview),
      error: () => undefined,
    });
  }
}
