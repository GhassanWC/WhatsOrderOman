import { Component, computed, inject, input, signal, effect } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PublicStoreService } from '../../core/services/public-store.service';
import { CartService } from '../../core/services/cart.service';
import { MarketplaceApiService } from '../../core/services/marketplace-api.service';
import { PublicProductDto, PublicStoreDto, ReviewDto } from '../../core/models/api-types';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { whatsAppLink } from '../../shared/whatsapp';
import { shareOrCopy } from '../../shared/share';
import { TranslationService } from '../../core/i18n/translation.service';
import { FavHeart, RatingBadge } from '../marketplace/cards';

@Component({
  selector: 'wo-store-page',
  imports: [RouterLink, FormsModule, BilingualPipe, OmrPricePipe, TranslatePipe, FavHeart, RatingBadge],
  template: `
    @if (notFound()) {
      <div class="not-found">
        <div class="emoji">🏜️</div>
        <h1>{{ 'store.notFoundTitle' | t }}</h1>
        <p class="muted">{{ 'store.notFoundText' | t }}</p>
      </div>
    } @else if (store(); as s) {
      <div class="page">
        <!-- ── Store header ──────────────────────────────────────────── -->
        <header class="hero" [class.hero--banner]="s.bannerUrl">
          @if (s.bannerUrl; as banner) {
            <img class="hero__bg" [src]="banner" alt="" />
          }
          <div class="hero__main container">
            <div class="logo">
              @if (s.logoUrl) {
                <img [src]="s.logoUrl" [alt]="s.name" />
              } @else {
                <span>{{ s.name.charAt(0) }}</span>
              }
            </div>
            <div class="info">
              <h1>{{ s.name | bilingual: s.nameAr }}</h1>
              @if (s.description || s.descriptionAr) {
                <p class="desc">{{ s.description | bilingual: s.descriptionAr }}</p>
              }
              <div class="badges">
                <span class="wo-chip" [class.wo-chip--open]="s.isOpenNow" [class.wo-chip--closed]="!s.isOpenNow">
                  ● {{ (s.isOpenNow ? 'store.open' : 'store.closed') | t }}
                </span>
                @if (s.rating !== null) {
                  <span class="rating-chip"><wo-rating [rating]="s.rating" [count]="s.reviewsCount" /></span>
                }
                @if (s.locationText) { <span class="loc">📍 {{ s.locationText }}</span> }
              </div>
              <div class="meta-row">
                @if (s.deliveryEnabled) {
                  <span>🛵 {{ s.deliveryFee === 0 ? ('market.freeDelivery' | t) : (s.deliveryFee | omr) }}</span>
                }
                @if (s.minimumOrderAmount > 0) {
                  <span>{{ 'store.minOrder' | t }} {{ s.minimumOrderAmount | omr }}</span>
                }
              </div>
              <div class="links">
                <wo-fav-heart kind="store" [refId]="s.id" />
                <button type="button" class="wo-btn wo-btn--ghost mini share" (click)="share(s)">
                  ↗ {{ 'common.share' | t }}
                </button>
                @if (s.whatsAppNumber) {
                  <a class="wo-btn wo-btn--whatsapp mini" [href]="waLink(s.whatsAppNumber)"
                     target="_blank" rel="noopener">WhatsApp</a>
                }
                @if (s.instagramHandle) {
                  <a class="wo-btn wo-btn--ghost mini" [href]="'https://instagram.com/' + s.instagramHandle"
                     target="_blank" rel="noopener">Instagram</a>
                }
              </div>
            </div>
          </div>
          @if (!s.acceptingOrders) {
            <div class="closed-banner container">⏸ {{ 'store.notAccepting' | t }}</div>
          }
        </header>

        <main class="container main">
          <!-- ── Running offers ──────────────────────────────────────── -->
          @if (s.offers.length > 0) {
            <div class="offers">
              @for (offer of s.offers; track offer.id) {
                <div class="offer">
                  <span class="offer__tag">
                    @switch (offer.type) {
                      @case ('Percentage') { -{{ offer.discountValue }}% }
                      @case ('FixedAmount') { -{{ offer.discountValue | omr }} }
                      @case ('FreeDelivery') { 🛵 {{ 'market.freeDelivery' | t }} }
                    }
                  </span>
                  <span class="offer__title">{{ offer.title | bilingual: offer.titleAr }}</span>
                  @if (offer.minimumOrderAmount > 0) {
                    <span class="offer__min">{{ 'market.offerMinOrder' | t: { amount: offer.minimumOrderAmount } }}</span>
                  }
                </div>
              }
            </div>
          }

          <!-- ── Search + categories ─────────────────────────────────── -->
          <div class="filter-bar">
            <input class="search" type="search" [placeholder]="'store.searchPlaceholder' | t"
                   [ngModel]="search()" (ngModelChange)="onSearch($event)" />
          </div>
          @if (s.categories.length > 0) {
            <div class="cats">
              <button type="button" class="cat" [class.cat--on]="!categoryId()" (click)="setCategory(null)">
                {{ 'store.all' | t }}
              </button>
              @for (category of s.categories; track category.id) {
                <button type="button" class="cat" [class.cat--on]="categoryId() === category.id"
                        (click)="setCategory(category.id)">
                  {{ category.name | bilingual: category.nameAr }}
                </button>
              }
            </div>
          }

          <!-- ── Featured ────────────────────────────────────────────── -->
          @if (!search() && !categoryId() && featured().length > 0) {
            <section class="featured">
              <h2>★ {{ 'store.featured' | t }}</h2>
              <div class="featured__scroll">
                @for (product of featured(); track product.id) {
                  <a class="fcard" [routerLink]="['/', s.slug, 'product', product.id]">
                    <div class="fcard__img">
                      @if (product.images.length > 0) {
                        <img [src]="product.images[0]" [alt]="product.name" loading="lazy" />
                      } @else {
                        <span>{{ product.name.charAt(0) }}</span>
                      }
                    </div>
                    <span class="fcard__name">{{ product.name | bilingual: product.nameAr }}</span>
                    <span class="fcard__price">
                      @if (product.variants.length > 0) { <small>{{ 'store.from' | t }}</small> }
                      {{ (product.discountedPrice ?? product.price) | omr }}
                    </span>
                  </a>
                }
              </div>
            </section>
          }

          <!-- ── Product grid ────────────────────────────────────────── -->
          @if (loading()) {
            <div class="grid">
              @for (i of [1, 2, 3, 4, 5, 6]; track i) {
                <div class="wo-skeleton" style="height: 210px"></div>
              }
            </div>
          } @else if (products().length === 0) {
            <div class="no-products muted">🔍 —</div>
          } @else {
            <div class="grid">
              @for (product of products(); track product.id) {
                <a class="pcard wo-card" [routerLink]="['/', s.slug, 'product', product.id]"
                   [class.pcard--out]="!product.inStock">
                  <div class="pcard__img">
                    @if (product.images.length > 0) {
                      <img [src]="product.images[0]" [alt]="product.name" loading="lazy" />
                    } @else {
                      <span class="ph">{{ product.name.charAt(0) }}</span>
                    }
                    @if (product.discountedPrice !== null) {
                      <span class="sale">-{{ discountPercent(product) }}%</span>
                    }
                    @if (!product.inStock) {
                      <span class="out">{{ 'store.soldOut' | t }}</span>
                    }
                  </div>
                  <div class="pcard__body">
                    <span class="pname">{{ product.name | bilingual: product.nameAr }}</span>
                    <div class="prices">
                      @if (product.variants.length > 0) { <small class="muted">{{ 'store.from' | t }}</small> }
                      <span class="price">{{ (product.discountedPrice ?? product.price) | omr }}</span>
                      @if (product.discountedPrice !== null) { <s class="was">{{ product.price | omr }}</s> }
                    </div>
                  </div>
                  <button type="button" class="add" [class.add--disabled]="!product.inStock || !s.acceptingOrders"
                          (click)="quickAdd($event, product)">＋</button>
                </a>
              }
            </div>
          }

          <!-- ── Reviews ─────────────────────────────────────────────── -->
          @if (reviews().length > 0) {
            <section class="reviews">
              <h2>{{ 'store.reviewsTitle' | t: { count: s.reviewsCount } }}</h2>
              <div class="review-list">
                @for (review of reviews(); track review.id) {
                  <div class="review wo-card">
                    <div class="review__head">
                      <b>{{ review.reviewerName }}</b>
                      <span class="stars">{{ '★★★★★'.slice(0, review.rating) }}</span>
                    </div>
                    @if (review.comment) { <p>{{ review.comment }}</p> }
                  </div>
                }
              </div>
            </section>
          }
        </main>

        <!-- ── Sticky cart bar ───────────────────────────────────────── -->
        @if (cart.count() > 0) {
          <a class="cart-bar" [routerLink]="['/', s.slug, 'cart']">
            <span class="count">{{ cart.count() }}</span>
            <span>{{ 'store.viewCart' | t }}</span>
            <span class="spacer"></span>
            <b>{{ cart.subtotal() | omr }}</b>
          </a>
        }
      </div>
    } @else {
      <div class="container" style="padding-top:32px">
        <div class="wo-skeleton" style="height: 140px; margin-bottom: 20px"></div>
        <div class="wo-skeleton" style="height: 400px"></div>
      </div>
    }
  `,
  styles: `
    .page { min-height: 100dvh; padding-bottom: 90px; }
    .not-found { min-height: 70dvh; display: grid; place-content: center; text-align: center; gap: 8px;
      .emoji { font-size: 54px; } }
    .hero { position: relative; background: linear-gradient(160deg, #0b7a63 0%, #0a5c4b 100%); color: #fff; padding-block: 22px; overflow: hidden; }
    .hero__bg { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
    .hero--banner::after { content: ''; position: absolute; inset: 0; background: linear-gradient(180deg, rgba(7,30,25,.45), rgba(7,30,25,.82)); }
    .hero__main { position: relative; z-index: 1; display: flex; gap: 18px; align-items: center; }
    .logo { width: 84px; height: 84px; border-radius: 22px; background: rgba(255,255,255,.15); overflow: hidden;
      display: grid; place-items: center; font: 800 34px var(--wo-font); flex-shrink: 0; border: 2px solid rgba(255,255,255,.35);
      img { width: 100%; height: 100%; object-fit: cover; } }
    .info { min-width: 0;
      h1 { font-size: 24px; }
      .desc { opacity: .9; font-size: 14px; margin-top: 3px; } }
    .badges { display: flex; gap: 10px; align-items: center; margin-top: 8px; flex-wrap: wrap;
      .loc { font-size: 13px; opacity: .9; } }
    .rating-chip { background: rgba(255,255,255,.92); border-radius: 999px; padding: 3px 10px; }
    .meta-row { display: flex; gap: 14px; margin-top: 8px; font-size: 13px; opacity: .92; flex-wrap: wrap; }
    .links { display: flex; gap: 8px; margin-top: 10px; align-items: center; flex-wrap: wrap;
      .mini { padding: 7px 16px; font-size: 13px; } }
    .share { background: rgba(255,255,255,.92); }
    .closed-banner { position: relative; z-index: 1; margin-top: 14px; background: rgba(0,0,0,.25); border-radius: 12px; padding: 10px 16px;
      font-weight: 600; font-size: 14px; }
    .main { padding-top: 20px; }
    .offers { display: grid; gap: 8px; margin-bottom: 16px; }
    .offer { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; background: #fff1f2;
      border: 1px solid #fecdd3; border-radius: var(--wo-radius-sm); padding: 9px 14px; font-size: 13.5px; }
    .offer__tag { background: #e11d48; color: #fff; font-weight: 800; border-radius: 999px; padding: 2px 10px; font-size: 12.5px; direction: ltr; }
    .offer__title { font-weight: 700; }
    .offer__min { color: var(--wo-muted); font-size: 12.5px; }
    .filter-bar { margin-bottom: 12px; }
    .search { width: 100%; border: 1.5px solid var(--wo-border); border-radius: 999px; padding: 12px 20px;
      font: 500 15px var(--wo-font); outline: none; background: var(--wo-surface);
      &:focus { border-color: var(--wo-primary); box-shadow: 0 0 0 3px var(--wo-primary-soft); } }
    .cats { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 6px; margin-bottom: 16px;
      scrollbar-width: none; &::-webkit-scrollbar { display: none; } }
    .cat { border: 1.5px solid var(--wo-border); background: var(--wo-surface); border-radius: 999px;
      padding: 8px 18px; font: 600 13.5px var(--wo-font); color: var(--wo-ink-soft); cursor: pointer; white-space: nowrap;
      &.cat--on { background: var(--wo-ink); border-color: var(--wo-ink); color: #fff; } }
    .featured { margin-bottom: 22px;
      h2 { font-size: 16px; margin-bottom: 10px; } }
    .featured__scroll { display: flex; gap: 12px; overflow-x: auto; padding-bottom: 8px;
      scrollbar-width: none; &::-webkit-scrollbar { display: none; } }
    .fcard { flex: 0 0 150px; border: none; background: var(--wo-surface); border-radius: var(--wo-radius);
      box-shadow: var(--wo-shadow); padding: 10px; cursor: pointer; text-align: start; font-family: var(--wo-font);
      display: grid; gap: 6px; text-decoration: none; color: inherit;
      &:hover { box-shadow: var(--wo-shadow-lift); } }
    .fcard__img { aspect-ratio: 1; border-radius: 10px; overflow: hidden; background: var(--wo-primary-soft);
      display: grid; place-items: center; font: 800 30px var(--wo-font); color: var(--wo-primary-dark);
      img { width: 100%; height: 100%; object-fit: cover; } }
    .fcard__name { font-weight: 600; font-size: 13.5px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .fcard__price { font-weight: 800; font-size: 13.5px; color: var(--wo-primary-dark);
      small { font-weight: 500; color: var(--wo-muted); } }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 14px;
      @media (min-width: 700px) { grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); } }
    .pcard { border: none; padding: 0; overflow: hidden; cursor: pointer; text-align: start;
      font-family: var(--wo-font); position: relative; display: flex; flex-direction: column;
      text-decoration: none; color: inherit;
      &:hover { box-shadow: var(--wo-shadow-lift); } }
    .pcard--out { opacity: .6; }
    .pcard__img { aspect-ratio: 4/3; background: var(--wo-primary-soft); position: relative; overflow: hidden;
      display: grid; place-items: center;
      img { width: 100%; height: 100%; object-fit: cover; }
      .ph { font: 800 38px var(--wo-font); color: var(--wo-primary-dark); }
      .sale { position: absolute; top: 8px; inset-inline-start: 8px; background: var(--wo-danger); color: #fff;
        border-radius: 999px; padding: 2px 9px; font: 700 11.5px var(--wo-font); }
      .out { position: absolute; inset: auto 0 0 0; background: rgba(15,23,42,.75); color: #fff; text-align: center;
        font: 600 12px var(--wo-font); padding: 4px; } }
    .pcard__body { padding: 10px 12px 12px; display: grid; gap: 4px;
      .pname { font-weight: 600; font-size: 14px; line-height: 1.3; }
      .prices { display: flex; align-items: baseline; gap: 6px; flex-wrap: wrap; }
      .price { font-weight: 800; color: var(--wo-primary-dark); }
      .was { color: var(--wo-muted); font-size: 12px; } }
    .add { position: absolute; bottom: 10px; inset-inline-end: 10px; width: 32px; height: 32px; border-radius: 50%;
      border: none; cursor: pointer;
      background: var(--wo-primary); color: #fff; display: grid; place-items: center; font-size: 18px; font-weight: 600;
      &.add--disabled { background: var(--wo-muted); } }
    .no-products { text-align: center; padding: 48px; font-size: 26px; }
    .reviews { margin-top: 30px;
      h2 { font-size: 17px; margin-bottom: 12px; } }
    .review-list { display: grid; gap: 10px; }
    .review { padding: 12px 16px;
      p { margin: 6px 0 0; font-size: 14px; color: var(--wo-ink-soft); } }
    .review__head { display: flex; justify-content: space-between; align-items: center; font-size: 14px; }
    .stars { color: #f59e0b; letter-spacing: 2px; }
    .cart-bar { position: fixed; bottom: 14px; inset-inline: 14px; max-width: 560px; margin-inline: auto;
      background: var(--wo-ink); color: #fff; border-radius: 999px; padding: 14px 22px; display: flex;
      align-items: center; gap: 12px; font: 600 15px var(--wo-font); box-shadow: var(--wo-shadow-lift); z-index: 30;
      .count { background: var(--wo-accent); color: #073b27; width: 26px; height: 26px; border-radius: 50%;
        display: grid; place-items: center; font-size: 13px; font-weight: 800; }
      &:hover { transform: translateY(-1px); } }
    @media (max-width: 760px) { .cart-bar { bottom: 76px; } }
  `,
})
export class StorePage {
  private readonly api = inject(PublicStoreService);
  private readonly marketplace = inject(MarketplaceApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);
  readonly cart = inject(CartService);

  /** Bound from the :slug route parameter. */
  readonly slug = input.required<string>();

  readonly store = signal<PublicStoreDto | null>(null);
  readonly products = signal<PublicProductDto[]>([]);
  readonly reviews = signal<ReviewDto[]>([]);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly search = signal('');
  readonly categoryId = signal<string | null>(null);

  readonly featured = computed(() => this.products().filter((p) => p.isFeatured));

  readonly waLink = whatsAppLink;

  private readonly search$ = new Subject<string>();
  private loadedSlug: string | null = null;

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.loadProducts());

    effect(() => {
      const slug = this.slug();
      if (slug && slug !== this.loadedSlug) {
        this.loadedSlug = slug;
        this.loadStore(slug);
      }
    });
  }

  private loadStore(slug: string): void {
    this.api.getStore(slug).subscribe({
      next: (store) => {
        this.store.set(store);
        this.cart.openStore(store);
        // Storefront follows the store's default language on first visit.
        if (!localStorage.getItem('wo.lang') && (store.defaultLanguage === 'ar' || store.defaultLanguage === 'en')) {
          void this.i18n.setLang(store.defaultLanguage);
        }
        this.loadProducts();
        if (store.reviewsCount > 0) {
          this.marketplace.storeReviews(slug, 1, 3).subscribe({
            next: (page) => this.reviews.set(page.items),
            error: () => undefined,
          });
        }
      },
      error: () => this.notFound.set(true),
    });
  }

  private loadProducts(): void {
    const slug = this.slug();
    this.loading.set(true);
    this.api.getProducts(slug, this.search() || undefined, this.categoryId() ?? undefined).subscribe({
      next: (products) => {
        this.products.set(products);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.search$.next(value);
  }

  setCategory(id: string | null): void {
    this.categoryId.set(id);
    this.loadProducts();
    const store = this.store();
    if (id && store) {
      this.marketplace.track({ eventType: 'CategoryViewed', storeId: store.id, productId: null, categoryId: id });
    }
  }

  discountPercent(product: PublicProductDto): number {
    if (product.discountedPrice === null || product.price === 0) return 0;
    return Math.round((1 - product.discountedPrice / product.price) * 100);
  }

  /** The ＋ button adds no-variant products instantly; others go to the product page. */
  quickAdd(event: Event, product: PublicProductDto): void {
    const store = this.store();
    if (!store) return;
    if (product.variants.length === 0 && product.inStock && store.acceptingOrders) {
      event.preventDefault();
      event.stopPropagation();
      this.cart.add(product, [], 1);
      this.marketplace.track({ eventType: 'AddedToCart', storeId: store.id, productId: product.id, categoryId: null });
      this.snackBar.open(this.i18n.t('store.addedToCart'), undefined, { duration: 1500 });
    }
    // With variants the click falls through to the product page link.
  }

  async share(store: PublicStoreDto): Promise<void> {
    const result = await shareOrCopy(store.name, `/${store.slug}`);
    if (result === 'copied') {
      this.snackBar.open(this.i18n.t('common.linkCopied'), undefined, { duration: 2000 });
    }
  }
}
