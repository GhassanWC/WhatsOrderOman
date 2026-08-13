import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../core/services/auth.service';
import { CartService } from '../../core/services/cart.service';
import { FavoritesService } from '../../core/services/favorites.service';
import { BuyerRealtimeService } from '../../core/realtime/realtime.service';
import { LangSwitcher } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

/**
 * The public marketplace layout: sticky header with search, desktop nav, a compact
 * mobile bottom bar, and the buyer's realtime connection (live notifications).
 * Wraps the homepage, discovery pages, the account area and every storefront.
 */
@Component({
  selector: 'wo-marketplace-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LangSwitcher, TranslatePipe],
  template: `
    <header class="topbar">
      <div class="container bar">
        <a routerLink="/" class="logo">🛒 <strong>WhatsOrder</strong></a>

        <form class="search" (submit)="submitSearch($event)">
          <input
            type="search"
            name="q"
            [value]="query()"
            (input)="query.set($any($event.target).value)"
            [placeholder]="'market.searchPlaceholder' | t"
          />
          <button type="submit" aria-label="search">🔍</button>
        </form>

        <nav class="links">
          <a routerLink="/stores" routerLinkActive="active">{{ 'market.nav.stores' | t }}</a>
          <a routerLink="/offers" routerLinkActive="active">{{ 'market.nav.offers' | t }}</a>
        </nav>

        <div class="actions">
          <a routerLink="/search" class="icon-btn mobile-only" aria-label="search">🔍</a>
          @if (cart.count() > 0 && cartLink(); as link) {
            <a [routerLink]="link" class="icon-btn cart" aria-label="cart">
              🛒<span class="badge">{{ cart.count() }}</span>
            </a>
          }
          <wo-lang-switcher />
          @if (auth.isAuthenticated()) {
            @if (auth.isSeller()) {
              <a routerLink="/dashboard" class="wo-btn wo-btn--ghost seller-link">
                {{ 'market.myStore' | t }}
              </a>
            }
            <a routerLink="/account" class="account-chip" [title]="auth.user()?.displayName">
              @if (auth.user()?.avatarUrl; as avatar) {
                <img [src]="avatar" alt="" />
              } @else {
                <span>{{ initial() }}</span>
              }
            </a>
          } @else {
            <a routerLink="/login" class="wo-btn wo-btn--ghost">{{ 'auth.login' | t }}</a>
            <a routerLink="/register" class="wo-btn wo-btn--primary register-btn">
              {{ 'auth.register' | t }}
            </a>
          }
        </div>
      </div>
    </header>

    <main class="content">
      <router-outlet />
    </main>

    <footer class="footer">
      <div class="container foot">
        <span>© {{ year }} WhatsOrder Oman</span>
        <a routerLink="/business">{{ 'market.sellOnWhatsOrder' | t }}</a>
      </div>
    </footer>

    <nav class="bottom-nav">
      <a routerLink="/" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="active">
        <span>🏠</span>{{ 'market.nav.home' | t }}
      </a>
      <a routerLink="/stores" routerLinkActive="active">
        <span>🏪</span>{{ 'market.nav.stores' | t }}
      </a>
      <a routerLink="/account/orders" routerLinkActive="active">
        <span>🧾</span>{{ 'market.nav.orders' | t }}
      </a>
      <a routerLink="/account/favorites" routerLinkActive="active">
        <span>♥</span>{{ 'market.nav.favorites' | t }}
      </a>
      <a routerLink="/account" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="active">
        <span>👤</span>{{ 'market.nav.account' | t }}
      </a>
    </nav>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 100dvh; background: var(--wo-bg); }
    .topbar {
      position: sticky; top: 0; z-index: 30; background: var(--wo-surface);
      border-bottom: 1px solid var(--wo-border);
    }
    .bar { display: flex; align-items: center; gap: 14px; height: 60px; }
    .logo { font-size: 17px; text-decoration: none; color: var(--wo-ink); white-space: nowrap; }
    .logo strong { color: var(--wo-primary); }
    .search {
      flex: 1; max-width: 460px; display: flex; background: var(--wo-bg);
      border: 1px solid var(--wo-border); border-radius: 99px; overflow: hidden;
    }
    .search input { flex: 1; border: none; background: transparent; padding: 9px 16px; font: inherit; outline: none; min-width: 0; }
    .search button { border: none; background: transparent; padding-inline: 12px; cursor: pointer; font-size: 15px; }
    .links { display: flex; gap: 4px; }
    .links a {
      text-decoration: none; color: var(--wo-ink-soft); font-weight: 600; font-size: 14px;
      padding: 7px 12px; border-radius: 9px;
    }
    .links a.active, .links a:hover { color: var(--wo-primary-dark); background: var(--wo-primary-soft); }
    .actions { display: flex; align-items: center; gap: 8px; margin-inline-start: auto; }
    .icon-btn { position: relative; display: grid; place-items: center; width: 38px; height: 38px; border-radius: 50%; text-decoration: none; font-size: 17px; }
    .icon-btn:hover { background: var(--wo-bg); }
    .badge {
      position: absolute; inset-block-start: 0; inset-inline-end: 0;
      background: var(--wo-danger); color: #fff; font-size: 10.5px; font-weight: 700;
      min-width: 17px; height: 17px; padding-inline: 4px; border-radius: 99px;
      display: grid; place-items: center;
    }
    .account-chip {
      display: grid; place-items: center; width: 38px; height: 38px; border-radius: 50%;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark); font-weight: 800;
      text-decoration: none; overflow: hidden;
    }
    .account-chip img { width: 100%; height: 100%; object-fit: cover; }
    .content { flex: 1; padding-bottom: 76px; }
    .footer { border-top: 1px solid var(--wo-border); background: var(--wo-surface); padding-bottom: 66px; }
    .foot { display: flex; justify-content: space-between; align-items: center; padding-block: 16px; font-size: 13px; color: var(--wo-ink-soft); }
    .foot a { color: var(--wo-primary-dark); font-weight: 600; text-decoration: none; }
    .bottom-nav {
      position: fixed; inset-inline: 0; bottom: 0; z-index: 30;
      display: none; background: var(--wo-surface); border-top: 1px solid var(--wo-border);
      padding: 6px 4px calc(6px + env(safe-area-inset-bottom));
    }
    .bottom-nav a {
      flex: 1; display: grid; justify-items: center; gap: 2px; text-decoration: none;
      color: var(--wo-muted); font-size: 11px; font-weight: 600; padding: 4px 0; border-radius: 10px;
    }
    .bottom-nav a span { font-size: 18px; line-height: 1; }
    .bottom-nav a.active { color: var(--wo-primary-dark); }
    .mobile-only { display: none; }

    @media (max-width: 760px) {
      .search, .links, .seller-link, .register-btn { display: none; }
      .mobile-only { display: grid; }
      .bottom-nav { display: flex; }
      .foot { padding-bottom: 8px; }
    }
    @media (min-width: 761px) {
      .content { padding-bottom: 0; }
      .footer { padding-bottom: 0; }
    }
  `,
})
export class MarketplaceShell {
  protected readonly auth = inject(AuthService);
  protected readonly cart = inject(CartService);
  private readonly favorites = inject(FavoritesService); // instantiated early so hearts are ready
  private readonly realtime = inject(BuyerRealtimeService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly query = signal('');
  readonly year = new Date().getFullYear();

  readonly initial = computed(() => (this.auth.user()?.displayName ?? '?').charAt(0).toUpperCase());
  readonly cartLink = computed(() => {
    const slug = this.cart.store()?.slug;
    return slug ? ['/', slug, 'cart'] : null;
  });

  constructor() {
    if (this.auth.isAuthenticated()) {
      this.realtime.start();
      this.realtime.notification$
        .pipe(takeUntilDestroyed())
        .subscribe((notification) => {
          // Machine tokens (status:X) are localized; free text is shown as-is.
          const body = notification.body.startsWith('status:')
            ? this.i18n.t('orders.status.' + notification.body.slice(7))
            : notification.body;
          this.snackBar.open(`${notification.title} — ${body}`, undefined, { duration: 4000 });
        });
    }
    this.destroyRef.onDestroy(() => this.realtime.stop());
  }

  submitSearch(event: Event): void {
    event.preventDefault();
    const q = this.query().trim();
    if (q) void this.router.navigate(['/search'], { queryParams: { q } });
  }
}
