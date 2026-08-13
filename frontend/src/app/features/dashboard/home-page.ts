import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { StoreApiService } from '../../core/services/store-api.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardSummaryDto } from '../../core/models/api-types';
import { StatusChip, EmptyState } from '../../shared/components';
import { OmrPricePipe, TranslatePipe } from '../../shared/pipes';

@Component({
  selector: 'wo-dashboard-home',
  imports: [RouterLink, DatePipe, StatusChip, EmptyState, OmrPricePipe, TranslatePipe],
  template: `
    <h1 class="greeting">{{ 'dash.greeting' | t: { name: auth.user()?.displayName ?? '' } }}</h1>

    @if (summary(); as s) {
      <div class="stats">
        <div class="stat wo-card">
          <span class="stat__label">{{ 'dash.ordersToday' | t }}</span>
          <span class="stat__value">{{ s.ordersToday }}</span>
        </div>
        <div class="stat wo-card">
          <span class="stat__label">{{ 'dash.revenueToday' | t }}</span>
          <span class="stat__value">{{ s.revenueToday | omr }}</span>
        </div>
        <div class="stat wo-card">
          <span class="stat__label">{{ 'dash.ordersMonth' | t }}</span>
          <span class="stat__value">{{ s.ordersThisMonth }}</span>
        </div>
        <div class="stat wo-card">
          <span class="stat__label">{{ 'dash.revenueMonth' | t }}</span>
          <span class="stat__value">{{ s.revenueThisMonth | omr }}</span>
        </div>
        <div class="stat wo-card">
          <span class="stat__label">{{ 'dash.avgOrder' | t }}</span>
          <span class="stat__value">{{ s.averageOrderValue | omr }}</span>
        </div>
        <a class="stat wo-card stat--action" routerLink="/dashboard/orders" [queryParams]="{ filter: 'new' }">
          <span class="stat__label">{{ 'dash.newOrders' | t }}</span>
          <span class="stat__value" [class.stat__value--alert]="s.newOrdersCount > 0">{{ s.newOrdersCount }}</span>
        </a>
      </div>

      <div class="grid">
        <section class="wo-card panel">
          <h2>{{ 'dash.topProducts' | t }}</h2>
          @if (s.topProducts.length === 0) {
            <wo-empty-state emoji="📈" messageKey="dash.noOrdersYet" />
          } @else {
            <ul class="top-list">
              @for (product of s.topProducts; track product.name; let i = $index) {
                <li>
                  <span class="rank">{{ i + 1 }}</span>
                  <span class="name">{{ product.name }}</span>
                  <span class="qty muted">{{ 'dash.sold' | t: { count: product.quantity } }}</span>
                  <span class="rev">{{ product.revenue | omr }}</span>
                </li>
              }
            </ul>
          }
        </section>

        <section class="wo-card panel">
          <div class="panel__head">
            <h2>{{ 'dash.recentOrders' | t }}</h2>
            <a routerLink="/dashboard/orders" class="view-all">{{ 'dash.viewAll' | t }} →</a>
          </div>
          @if (s.recentOrders.length === 0) {
            <wo-empty-state emoji="🧾" messageKey="dash.noOrdersYet" />
          } @else {
            <ul class="orders-list">
              @for (order of s.recentOrders; track order.id) {
                <li>
                  <div class="line1">
                    <span class="num">#{{ order.orderNumber }}</span>
                    <wo-status-chip [status]="order.status" />
                    <span class="spacer"></span>
                    <span class="total">{{ order.total | omr }}</span>
                  </div>
                  <div class="line2 muted">
                    {{ order.customerName }} · {{ order.itemsSummary }} · {{ order.createdAt | date: 'short' }}
                  </div>
                </li>
              }
            </ul>
          }
        </section>
      </div>
    } @else {
      <div class="stats">
        @for (i of [1, 2, 3, 4, 5, 6]; track i) {
          <div class="stat wo-skeleton" style="height: 86px"></div>
        }
      </div>
    }
  `,
  styles: `
    .greeting { font-size: 22px; margin-bottom: 20px; }
    .stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 14px; margin-bottom: 22px; }
    .stat { padding: 16px 18px; display: grid; gap: 4px; }
    .stat--action { cursor: pointer; &:hover { box-shadow: var(--wo-shadow-lift); } }
    .stat__label { font-size: 12.5px; font-weight: 600; color: var(--wo-ink-soft); }
    .stat__value { font-size: 22px; font-weight: 800; letter-spacing: -.02em; }
    .stat__value--alert { color: var(--wo-primary); }
    .grid { display: grid; gap: 16px; grid-template-columns: 1fr 1.4fr;
      @media (max-width: 900px) { grid-template-columns: 1fr; } }
    .panel { padding: 20px; h2 { font-size: 16px; margin-bottom: 14px; } }
    .panel__head { display: flex; justify-content: space-between; align-items: baseline;
      .view-all { font-size: 13.5px; font-weight: 600; } }
    .top-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px;
      li { display: flex; align-items: center; gap: 10px; }
      .rank { width: 24px; height: 24px; border-radius: 8px; background: var(--wo-primary-soft);
        color: var(--wo-primary-dark); font: 700 12.5px var(--wo-font); display: grid; place-items: center; }
      .name { font-weight: 600; flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .qty { font-size: 12.5px; }
      .rev { font-weight: 700; font-size: 13.5px; } }
    .orders-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 12px;
      li { border-bottom: 1px solid var(--wo-border); padding-bottom: 12px; &:last-child { border: none; padding-bottom: 0; } }
      .line1 { display: flex; align-items: center; gap: 10px; }
      .num { font-weight: 700; }
      .total { font-weight: 700; }
      .line2 { font-size: 13px; margin-top: 3px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; } }
  `,
})
export class DashboardHomePage {
  readonly auth = inject(AuthService);
  private readonly api = inject(StoreApiService);

  readonly summary = signal<DashboardSummaryDto | null>(null);

  constructor() {
    this.api.getDashboardSummary().subscribe((summary) => this.summary.set(summary));
  }
}
