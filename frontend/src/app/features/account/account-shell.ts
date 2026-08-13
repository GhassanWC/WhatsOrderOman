import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TranslatePipe } from '../../shared/pipes';

/**
 * The /account layout inside the marketplace shell: sidebar navigation on desktop,
 * a horizontally scrolling tab bar on mobile.
 */
@Component({
  selector: 'wo-account-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <div class="container layout">
      <aside class="side">
        <div class="who">
          <div class="avatar">
            @if (auth.user()?.avatarUrl; as avatar) {
              <img [src]="avatar" alt="" />
            } @else {
              <span>{{ (auth.user()?.displayName ?? '?').charAt(0) }}</span>
            }
          </div>
          <div class="who-info">
            <b>{{ auth.user()?.displayName }}</b>
            <small>{{ auth.user()?.email }}</small>
          </div>
        </div>
        <nav class="nav">
          <a routerLink="/account" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="on">
            <span>🏠</span>{{ 'account.nav.overview' | t }}
          </a>
          <a routerLink="/account/orders" routerLinkActive="on"><span>🧾</span>{{ 'account.nav.orders' | t }}</a>
          <a routerLink="/account/messages" routerLinkActive="on"><span>💬</span>{{ 'account.nav.messages' | t }}</a>
          <a routerLink="/account/favorites" routerLinkActive="on"><span>♥</span>{{ 'account.nav.favorites' | t }}</a>
          <a routerLink="/account/offers" routerLinkActive="on"><span>🏷️</span>{{ 'account.nav.offers' | t }}</a>
          <a routerLink="/account/addresses" routerLinkActive="on"><span>📍</span>{{ 'account.nav.addresses' | t }}</a>
          <a routerLink="/account/notifications" routerLinkActive="on"><span>🔔</span>{{ 'account.nav.notifications' | t }}</a>
          <a routerLink="/account/profile" routerLinkActive="on"><span>👤</span>{{ 'account.nav.profile' | t }}</a>
          <a routerLink="/account/payments" routerLinkActive="on"><span>💳</span>{{ 'account.nav.payments' | t }}</a>
          <a routerLink="/account/billing" routerLinkActive="on"><span>📄</span>{{ 'account.nav.billing' | t }}</a>
          <button type="button" class="logout" (click)="auth.logout()">
            <span>🚪</span>{{ 'account.logout' | t }}
          </button>
        </nav>
      </aside>
      <section class="content">
        <router-outlet />
      </section>
    </div>
  `,
  styles: `
    .layout { display: grid; grid-template-columns: 230px minmax(0, 1fr); gap: 22px; padding-block: 22px 50px; align-items: start; }
    .side { position: sticky; top: 76px; display: grid; gap: 14px; }
    .who { display: flex; align-items: center; gap: 10px; padding: 4px; }
    .avatar { width: 44px; height: 44px; border-radius: 50%; overflow: hidden;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      display: grid; place-items: center; font-weight: 800; font-size: 18px; flex-shrink: 0; }
    .avatar img { width: 100%; height: 100%; object-fit: cover; }
    .who-info { display: grid; min-width: 0;
      b { font-size: 14.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
      small { color: var(--wo-muted); font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; } }
    .nav { display: grid; gap: 2px; }
    .nav a, .logout {
      display: flex; align-items: center; gap: 10px; text-decoration: none; color: var(--wo-ink-soft);
      font: 600 14px var(--wo-font); padding: 9px 12px; border-radius: 10px; border: none;
      background: transparent; cursor: pointer; text-align: start;
    }
    .nav a span, .logout span { width: 20px; text-align: center; }
    .nav a:hover, .logout:hover { background: var(--wo-surface); }
    .nav a.on { background: var(--wo-primary-soft); color: var(--wo-primary-dark); }
    .logout { color: var(--wo-danger); margin-top: 6px; }
    .content { min-width: 0; }

    @media (max-width: 860px) {
      .layout { grid-template-columns: 1fr; padding-top: 12px; }
      .side { position: static; }
      .who { display: none; }
      .nav {
        display: flex; overflow-x: auto; gap: 6px; padding-bottom: 6px;
        scrollbar-width: none;
      }
      .nav::-webkit-scrollbar { display: none; }
      .nav a, .logout { white-space: nowrap; background: var(--wo-surface); border: 1px solid var(--wo-border); border-radius: 999px; padding: 8px 14px; }
      .nav a.on { border-color: var(--wo-primary); }
      .logout { margin-top: 0; }
    }
  `,
})
export class AccountShell {
  protected readonly auth = inject(AuthService);
}
