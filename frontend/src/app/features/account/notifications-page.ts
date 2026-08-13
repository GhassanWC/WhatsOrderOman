import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountApiService } from '../../core/services/account-api.service';
import { NotificationDto } from '../../core/models/api-types';
import { EmptyState } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'wo-account-notifications',
  imports: [DatePipe, RouterLink, TranslatePipe, EmptyState],
  template: `
    <div class="head">
      <h1>{{ 'account.nav.notifications' | t }}</h1>
      @if (unread() > 0) {
        <button class="wo-btn wo-btn--ghost mini" (click)="markAllRead()">
          {{ 'notifications.markAllRead' | t }}
        </button>
      }
    </div>

    @if (!loaded()) {
      <div class="wo-skeleton" style="height: 70px; margin-bottom: 8px"></div>
      <div class="wo-skeleton" style="height: 70px"></div>
    } @else if (items().length === 0) {
      <wo-empty-state emoji="🔔" messageKey="account.noNotifications" />
    } @else {
      <div class="list">
        @for (notification of items(); track notification.id) {
          <a class="row wo-card" [class.unread]="!notification.isRead"
             [routerLink]="notification.orderId ? ['/account/orders', notification.orderId] : null">
            <span class="icon">
              @switch (notification.type) {
                @case ('OrderStatusChanged') { 📦 }
                @case ('NewMessage') { 💬 }
                @default { 🔔 }
              }
            </span>
            <div class="mid">
              <b>{{ notification.title }}</b>
              <small class="muted">{{ localizedBody(notification) }}</small>
            </div>
            <small class="when muted">{{ notification.createdAt | date: 'short' }}</small>
          </a>
        }
      </div>
    }
  `,
  styles: `
    .head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px;
      h1 { margin: 0; font-size: 22px; font-weight: 800; } }
    .mini { padding: 6px 12px; font-size: 12.5px; }
    .list { display: grid; gap: 8px; }
    .row { display: flex; align-items: center; gap: 12px; padding: 12px 14px;
      text-decoration: none; color: inherit; }
    .row.unread { border-inline-start: 3px solid var(--wo-primary); }
    .icon { font-size: 20px; }
    .mid { flex: 1; min-width: 0; display: grid;
      b { font-size: 14px; }
      small { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; } }
    .when { white-space: nowrap; font-size: 12px; }
  `,
})
export class AccountNotificationsPage {
  private readonly api = inject(AccountApiService);
  private readonly i18n = inject(TranslationService);

  readonly loaded = signal(false);
  readonly items = signal<NotificationDto[]>([]);
  readonly unread = signal(0);

  constructor() {
    this.api.notifications(50).subscribe({
      next: (page) => {
        this.items.set(page.items);
        this.unread.set(page.unreadCount);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }

  localizedBody(notification: NotificationDto): string {
    return notification.body.startsWith('status:')
      ? this.i18n.t('orders.status.' + notification.body.slice(7))
      : notification.body;
  }

  markAllRead(): void {
    this.api.markNotificationsRead().subscribe({
      next: () => {
        this.unread.set(0);
        this.items.update((list) => list.map((n) => ({ ...n, isRead: true })));
      },
    });
  }
}
