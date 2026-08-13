import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
import { StoreApiService } from '../../core/services/store-api.service';
import { NotificationDto, StoreDto } from '../../core/models/api-types';
import { OwnerRealtimeService } from '../../core/realtime/realtime.service';
import { LangSwitcher } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

/** Dashboard layout: top bar + responsive side navigation (bottom bar on mobile). */
@Component({
  selector: 'wo-dashboard-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, DatePipe, LangSwitcher, TranslatePipe],
  template: `
    <div class="shell">
      <header class="topbar">
        <div class="topbar__inner">
          <a routerLink="/dashboard" class="logo">Whats<span>Order</span></a>

          @if (store(); as s) {
            <button type="button" class="store-link" (click)="copyLink()" [title]="'nav.copyLink' | t">
              <span class="dot" [class.dot--on]="s.isAcceptingOrders"></span>
              /{{ s.slug }} ⧉
            </button>
          }

          <span class="spacer"></span>

          <!-- ── Notifications bell ─────────────────────────────────── -->
          <div class="bell-wrap">
            <button type="button" class="bell" (click)="toggleBell()" [title]="'notifications.title' | t">
              🔔
              @if (unreadCount() > 0) {
                <span class="badge">{{ unreadCount() > 99 ? '99+' : unreadCount() }}</span>
              }
            </button>
            @if (bellOpen()) {
              <div class="panel wo-card">
                <div class="panel__head">
                  <b>{{ 'notifications.title' | t }}</b>
                  <span class="live" [class.live--on]="realtime.connected()">●</span>
                </div>
                @if (notifications().length === 0) {
                  <p class="panel__empty">{{ 'notifications.empty' | t }}</p>
                } @else {
                  <div class="panel__list">
                    @for (n of notifications(); track n.id) {
                      <button type="button" class="notif" [class.notif--unread]="!n.isRead" (click)="open(n)">
                        <span class="icon">{{ icon(n) }}</span>
                        <span class="text">
                          <b>{{ n.title }}</b>
                          <small>{{ n.body }}</small>
                          <small class="time">{{ n.createdAt | date: 'MMM d, h:mm a' }}</small>
                        </span>
                      </button>
                    }
                  </div>
                }
              </div>
            }
          </div>

          @if (store(); as s) {
            <a class="view-store" [href]="'/' + s.slug" target="_blank" rel="noopener">
              {{ 'nav.viewStore' | t }} ↗
            </a>
          }
          <wo-lang-switcher />
          <button type="button" class="logout" (click)="logout()">{{ 'auth.logout' | t }}</button>
        </div>
      </header>

      <div class="body">
        <nav class="nav">
          <a routerLink="/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
            <span class="icon">📊</span>{{ 'nav.home' | t }}
          </a>
          <a routerLink="/dashboard/orders" routerLinkActive="active">
            <span class="icon">🧾</span>{{ 'nav.orders' | t }}
          </a>
          <a routerLink="/dashboard/products" routerLinkActive="active">
            <span class="icon">🛍️</span>{{ 'nav.products' | t }}
          </a>
          <a routerLink="/dashboard/offers" routerLinkActive="active">
            <span class="icon">🏷️</span>{{ 'nav.offers' | t }}
          </a>
          <a routerLink="/dashboard/reviews" routerLinkActive="active">
            <span class="icon">⭐</span>{{ 'nav.reviews' | t }}
          </a>
          <a routerLink="/dashboard/settings" routerLinkActive="active">
            <span class="icon">⚙️</span>{{ 'nav.settings' | t }}
          </a>
        </nav>

        <main class="content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: `
    .shell { min-height: 100dvh; display: flex; flex-direction: column; }
    .topbar { background: var(--wo-surface); border-bottom: 1px solid var(--wo-border); position: sticky; top: 0; z-index: 20; }
    .topbar__inner { max-width: 1240px; margin-inline: auto; display: flex; align-items: center; gap: 14px; padding: 12px 20px; }
    .logo { font: 800 20px var(--wo-font); color: var(--wo-ink); span { color: var(--wo-primary); } }
    .store-link { display: inline-flex; align-items: center; gap: 7px; border: 1px solid var(--wo-border);
      background: var(--wo-bg); border-radius: 999px; padding: 5px 13px; font: 600 13px var(--wo-font);
      color: var(--wo-ink-soft); cursor: pointer; direction: ltr;
      .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--wo-muted); }
      .dot--on { background: #22c55e; } }
    .bell-wrap { position: relative; }
    .bell { border: none; background: none; font-size: 19px; cursor: pointer; position: relative; padding: 4px;
      .badge { position: absolute; top: -4px; inset-inline-end: -8px; background: var(--wo-danger, #dc2626);
        color: #fff; border-radius: 999px; font: 700 10.5px var(--wo-font); padding: 2px 6px; } }
    .panel { position: absolute; top: 40px; inset-inline-end: 0; width: min(340px, 86vw); z-index: 30;
      padding: 0; overflow: hidden; box-shadow: 0 12px 32px rgba(15, 40, 30, .14); }
    .panel__head { display: flex; align-items: center; justify-content: space-between;
      padding: 12px 16px; border-bottom: 1px solid var(--wo-border); font-size: 14px; }
    .live { color: var(--wo-muted); font-size: 11px; }
    .live--on { color: #22c55e; }
    .panel__empty { padding: 24px 16px; text-align: center; color: var(--wo-muted); font-size: 13.5px; }
    .panel__list { max-height: 380px; overflow-y: auto; }
    .notif { display: flex; width: 100%; gap: 10px; padding: 11px 16px; border: none; background: none;
      cursor: pointer; text-align: start; border-bottom: 1px solid var(--wo-bg); font-family: var(--wo-font);
      &:hover { background: var(--wo-bg); }
      .icon { font-size: 17px; }
      .text { display: grid; gap: 1px; min-width: 0;
        b { font-size: 13.5px; }
        small { font-size: 12px; color: var(--wo-ink-soft); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .time { color: var(--wo-muted); font-size: 11px; } } }
    .notif--unread { background: var(--wo-primary-soft);
      &:hover { background: var(--wo-primary-soft); } }
    .view-store { font: 600 13.5px var(--wo-font); }
    .logout { border: none; background: none; color: var(--wo-ink-soft); font: 600 13.5px var(--wo-font); cursor: pointer;
      &:hover { color: var(--wo-danger); } }
    .body { flex: 1; display: flex; max-width: 1240px; margin-inline: auto; width: 100%; }
    .nav { display: flex; flex-direction: column; gap: 4px; padding: 20px 12px; width: 210px; flex-shrink: 0;
      a { display: flex; align-items: center; gap: 10px; padding: 10px 14px; border-radius: 10px;
        color: var(--wo-ink-soft); font-weight: 600; font-size: 14.5px;
        .icon { font-size: 17px; }
        &:hover { background: #eef1f5; }
        &.active { background: var(--wo-primary-soft); color: var(--wo-primary-dark); } } }
    .content { flex: 1; padding: 24px 20px 96px; min-width: 0; }

    @media (max-width: 760px) {
      .view-store { display: none; }
      .nav { position: fixed; bottom: 0; inset-inline: 0; width: auto; flex-direction: row;
        background: var(--wo-surface); border-top: 1px solid var(--wo-border); padding: 6px 8px;
        justify-content: space-around; z-index: 20;
        a { flex-direction: column; gap: 2px; font-size: 11px; padding: 6px 10px; } }
      .content { padding-bottom: 110px; }
    }
  `,
})
export class DashboardShell {
  readonly auth = inject(AuthService);
  private readonly api = inject(StoreApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);
  private readonly router = inject(Router);
  readonly realtime = inject(OwnerRealtimeService);

  readonly store = signal<StoreDto | null>(null);
  readonly notifications = signal<NotificationDto[]>([]);
  readonly unreadCount = signal(0);
  readonly bellOpen = signal(false);

  constructor() {
    this.api.getMyStore().subscribe({
      next: (store) => this.store.set(store),
      error: () => undefined,
    });

    // One realtime connection for the whole dashboard.
    this.realtime.start();
    this.loadNotifications();

    this.realtime.notification$.pipe(takeUntilDestroyed()).subscribe((n) => {
      this.notifications.update((list) => [n, ...list].slice(0, 30));
      this.unreadCount.update((count) => count + 1);

      const text = `${this.i18n.t('notifications.type.' + n.type)} — ${n.title}`;
      this.snackBar.open(text, this.i18n.t('nav.orders'), { duration: 5000 })
        .onAction().subscribe(() => void this.router.navigate(['/dashboard/orders']));
      this.pushBrowserNotification(text, n.body);
    });

    this.realtime.reconnected$.pipe(takeUntilDestroyed()).subscribe(() => this.loadNotifications());
  }

  toggleBell(): void {
    const opening = !this.bellOpen();
    this.bellOpen.set(opening);
    if (opening) {
      // Ask for browser-notification permission on an explicit user gesture.
      if ('Notification' in window && Notification.permission === 'default') {
        void Notification.requestPermission();
      }
      if (this.unreadCount() > 0) {
        this.api.markNotificationsRead().subscribe(() => {
          this.unreadCount.set(0);
          this.notifications.update((list) => list.map((n) => ({ ...n, isRead: true })));
        });
      }
    }
  }

  open(notification: NotificationDto): void {
    this.bellOpen.set(false);
    void this.router.navigate(['/dashboard/orders']);
  }

  icon(notification: NotificationDto): string {
    switch (notification.type) {
      case 'NewOrder': return '🧾';
      case 'NewMessage': return '💬';
      default: return '🔄';
    }
  }

  logout(): void {
    this.realtime.stop();
    this.auth.logout();
  }

  copyLink(): void {
    const slug = this.store()?.slug;
    if (!slug) return;
    void navigator.clipboard.writeText(`${location.origin}/${slug}`).then(() => {
      this.snackBar.open(this.i18n.t('common.copied'), undefined, { duration: 2000 });
    });
  }

  private loadNotifications(): void {
    this.api.getNotifications().subscribe({
      next: (page) => {
        this.notifications.set(page.items);
        this.unreadCount.set(page.unreadCount);
      },
      error: () => undefined,
    });
  }

  /** Best-effort PWA-style alert when the tab is in the background. */
  private pushBrowserNotification(title: string, body: string): void {
    if (!('Notification' in window) || Notification.permission !== 'granted' || !document.hidden) return;
    try {
      new Notification(title, { body, icon: '/favicon.ico' });
    } catch {
      // Some platforms (Android) only allow notifications via a service worker — fine.
    }
  }
}
