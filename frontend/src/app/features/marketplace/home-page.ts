import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { MarketplaceApiService } from '../../core/services/marketplace-api.service';
import { HomeSectionDto } from '../../core/models/api-types';
import { TranslatePipe } from '../../shared/pipes';
import { CardSkeletons, OfferCard, ProductCard, StoreCard } from './cards';

/**
 * The marketplace homepage: hero search plus server-composed discovery sections
 * (personalized for signed-in buyers, popularity-based for visitors).
 */
@Component({
  selector: 'wo-marketplace-home',
  imports: [RouterLink, TranslatePipe, StoreCard, ProductCard, OfferCard, CardSkeletons],
  template: `
    <section class="hero">
      <div class="container">
        <h1>{{ 'market.heroTitle' | t }}</h1>
        <p>{{ 'market.heroSubtitle' | t }}</p>
        <form class="hero-search" (submit)="submitSearch($event)">
          <input
            type="search"
            [value]="query()"
            (input)="query.set($any($event.target).value)"
            [placeholder]="'market.searchPlaceholder' | t"
          />
          <button type="submit" class="wo-btn wo-btn--primary">{{ 'market.searchAction' | t }}</button>
        </form>
      </div>
    </section>

    <div class="container sections">
      @if (loading()) {
        <div class="grid stores"><wo-card-skeletons [count]="4" /></div>
        <div class="grid products"><wo-card-skeletons [count]="6" /></div>
      } @else {
        @for (section of sections(); track section.key) {
          <section class="section">
            <header class="section-head">
              <h2>{{ 'market.section.' + section.key | t }}</h2>
              @if (section.stores) {
                <a routerLink="/stores" class="see-all">{{ 'market.seeAll' | t }}</a>
              } @else if (section.offers) {
                <a routerLink="/offers" class="see-all">{{ 'market.seeAll' | t }}</a>
              }
            </header>

            @if (section.stores; as stores) {
              <div class="rail">
                @for (store of stores; track store.id) {
                  <wo-store-card class="rail-store" [store]="store" />
                }
              </div>
            }
            @if (section.products; as products) {
              <div class="rail">
                @for (product of products; track product.id) {
                  <wo-product-card class="rail-product" [product]="product" />
                }
              </div>
            }
            @if (section.offers; as offers) {
              <div class="offer-grid">
                @for (offer of offers; track offer.id) {
                  <wo-offer-card [offer]="offer" />
                }
              </div>
            }
          </section>
        } @empty {
          <div class="empty">
            <span class="empty-emoji">🏪</span>
            <p>{{ 'market.emptyHome' | t }}</p>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .hero {
      background: linear-gradient(135deg, var(--wo-primary) 0%, var(--wo-primary-dark) 100%);
      color: #fff; padding: 38px 0 42px; text-align: center;
    }
    .hero h1 { margin: 0; font-size: clamp(24px, 4.5vw, 36px); font-weight: 800; }
    .hero p { margin: 8px auto 20px; opacity: .92; max-width: 560px; }
    .hero-search {
      display: flex; gap: 8px; max-width: 540px; margin: 0 auto; background: #fff;
      padding: 6px; border-radius: 99px; box-shadow: var(--wo-shadow-lift);
    }
    .hero-search input { flex: 1; border: none; outline: none; font: inherit; padding: 8px 16px; border-radius: 99px; min-width: 0; }
    .hero-search .wo-btn { border-radius: 99px; }
    .sections { padding-block: 26px 40px; display: grid; gap: 30px; }
    .section-head { display: flex; align-items: baseline; justify-content: space-between; margin-bottom: 12px; }
    .section-head h2 { margin: 0; font-size: 19px; font-weight: 800; }
    .see-all { font-size: 13.5px; font-weight: 600; color: var(--wo-primary-dark); text-decoration: none; }
    .rail {
      display: grid; grid-auto-flow: column; grid-auto-columns: min(78vw, 300px);
      gap: 14px; overflow-x: auto; padding: 2px 2px 10px; scroll-snap-type: x proximity;
      scrollbar-width: thin;
    }
    .rail > * { scroll-snap-align: start; }
    .rail-product { grid-auto-columns: unset; }
    .rail:has(.rail-product) { grid-auto-columns: min(46vw, 190px); }
    .offer-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 12px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 14px; }
    .grid.products { grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); }
    .empty { text-align: center; padding: 60px 0; color: var(--wo-ink-soft); }
    .empty-emoji { font-size: 42px; display: block; margin-bottom: 10px; }
  `,
})
export class MarketplaceHomePage {
  private readonly api = inject(MarketplaceApiService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly sections = signal<HomeSectionDto[]>([]);
  readonly query = signal('');

  constructor() {
    this.api.home().subscribe({
      next: (home) => {
        this.sections.set(home.sections);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  submitSearch(event: Event): void {
    event.preventDefault();
    const q = this.query().trim();
    if (q) void this.router.navigate(['/search'], { queryParams: { q } });
  }
}
