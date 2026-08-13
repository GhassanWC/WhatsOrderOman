import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FavoritesService } from '../../core/services/favorites.service';
import { ProductCardDto, PublicOfferDto, StoreCardDto } from '../../core/models/api-types';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';

/** Compact star rating: ★ 4.5 (12). Hidden entirely when there are no reviews. */
@Component({
  selector: 'wo-rating',
  imports: [],
  template: `
    @if (count() > 0) {
      <span class="rating" [title]="rating() + ' / 5'">
        <span class="star">★</span>{{ rating() }}
        <span class="count">({{ count() }})</span>
      </span>
    }
  `,
  styles: `
    .rating { display: inline-flex; align-items: center; gap: 3px; font-size: 13px; font-weight: 600; color: var(--wo-ink); }
    .star { color: #f59e0b; }
    .count { color: var(--wo-muted); font-weight: 400; }
  `,
})
export class RatingBadge {
  readonly rating = input<number | null>(null);
  readonly count = input(0);
}

/**
 * Heart toggle for stores/products. Signed-out users are sent to login with a
 * return URL, so the tap is never lost silently.
 */
@Component({
  selector: 'wo-fav-heart',
  imports: [],
  template: `
    <button
      type="button"
      class="heart"
      [class.on]="isFavorite()"
      (click)="toggle($event)"
      [attr.aria-pressed]="isFavorite()"
      [attr.aria-label]="'favorite'"
    >
      {{ isFavorite() ? '♥' : '♡' }}
    </button>
  `,
  styles: `
    .heart {
      display: grid; place-items: center; width: 34px; height: 34px;
      border-radius: 50%; border: none; cursor: pointer; font-size: 18px; line-height: 1;
      background: color-mix(in srgb, var(--wo-surface) 88%, transparent);
      color: var(--wo-ink-soft); box-shadow: var(--wo-shadow);
      transition: transform .12s, color .12s;
    }
    .heart:active { transform: scale(1.15); }
    .heart.on { color: #e11d48; }
  `,
})
export class FavHeart {
  private readonly favorites = inject(FavoritesService);
  private readonly router = inject(Router);

  readonly kind = input.required<'store' | 'product'>();
  readonly refId = input.required<string>();

  readonly isFavorite = computed(() =>
    this.kind() === 'store'
      ? this.favorites.storeFavorite(this.refId())()
      : this.favorites.productFavorite(this.refId())(),
  );

  toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const ok =
      this.kind() === 'store'
        ? this.favorites.toggleStore(this.refId())
        : this.favorites.toggleProduct(this.refId());
    if (!ok) {
      void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
    }
  }
}

@Component({
  selector: 'wo-store-card',
  imports: [RouterLink, TranslatePipe, BilingualPipe, OmrPricePipe, RatingBadge, FavHeart],
  template: `
    <a class="card" [routerLink]="['/', store().slug]">
      <div class="banner" [class.has-image]="store().bannerUrl">
        @if (store().bannerUrl; as banner) {
          <img [src]="banner" alt="" loading="lazy" />
        }
        <div class="badges">
          @if (store().hasActiveOffers) {
            <span class="badge badge--offer">{{ 'market.offerBadge' | t }}</span>
          }
          @if (store().isNew) {
            <span class="badge badge--new">{{ 'market.newBadge' | t }}</span>
          }
        </div>
        <wo-fav-heart class="fav" kind="store" [refId]="store().id" />
      </div>
      <div class="body">
        <div class="logo">
          @if (store().logoUrl; as logo) {
            <img [src]="logo" alt="" loading="lazy" />
          } @else {
            <span>{{ store().name.charAt(0) }}</span>
          }
        </div>
        <div class="info">
          <h3>{{ store().name | bilingual: store().nameAr }}</h3>
          <p class="meta">
            <wo-rating [rating]="store().rating" [count]="store().reviewsCount" />
            @if (store().wilayat || store().governorate) {
              <span class="dot">·</span><span>{{ store().wilayat || store().governorate }}</span>
            }
          </p>
          <p class="meta small">
            <span [class.open]="store().isOpenNow" [class.closed]="!store().isOpenNow">
              {{ (store().isOpenNow ? 'store.open' : 'store.closed') | t }}
            </span>
            @if (store().deliveryEnabled) {
              <span class="dot">·</span>
              <span>🛵 {{ store().deliveryFee === 0 ? ('market.freeDelivery' | t) : (store().deliveryFee | omr) }}</span>
            }
          </p>
        </div>
      </div>
    </a>
  `,
  styles: `
    .card {
      display: block; background: var(--wo-surface); border-radius: var(--wo-radius);
      border: 1px solid var(--wo-border); overflow: hidden; text-decoration: none; color: inherit;
      transition: box-shadow .15s, transform .15s;
    }
    .card:hover { box-shadow: var(--wo-shadow-lift); transform: translateY(-2px); }
    .banner {
      position: relative; aspect-ratio: 3 / 1.15; background:
        linear-gradient(120deg, var(--wo-primary) 0%, var(--wo-primary-dark) 100%);
    }
    .banner img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .badges { position: absolute; inset-block-start: 8px; inset-inline-start: 8px; display: flex; gap: 6px; }
    .badge { font-size: 11px; font-weight: 700; padding: 3px 8px; border-radius: 99px; color: #fff; }
    .badge--offer { background: #e11d48; }
    .badge--new { background: #2563eb; }
    .fav { position: absolute; inset-block-start: 8px; inset-inline-end: 8px; }
    .body { display: flex; gap: 10px; padding: 10px 12px 12px; }
    .logo {
      flex: 0 0 auto; width: 44px; height: 44px; margin-top: -26px; border-radius: 12px;
      background: var(--wo-surface); border: 2px solid var(--wo-surface); box-shadow: var(--wo-shadow);
      display: grid; place-items: center; overflow: hidden; font-weight: 800; color: var(--wo-primary);
      position: relative; z-index: 1;
    }
    .logo img { width: 100%; height: 100%; object-fit: cover; }
    .info { min-width: 0; }
    h3 { margin: 0; font-size: 15px; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .meta { margin: 3px 0 0; font-size: 13px; color: var(--wo-ink-soft); display: flex; align-items: center; gap: 5px; flex-wrap: wrap; }
    .meta.small { font-size: 12.5px; }
    .dot { color: var(--wo-muted); }
    .open { color: var(--wo-primary); font-weight: 600; }
    .closed { color: var(--wo-danger); font-weight: 600; }
  `,
})
export class StoreCard {
  readonly store = input.required<StoreCardDto>();
}

@Component({
  selector: 'wo-product-card',
  imports: [RouterLink, BilingualPipe, OmrPricePipe, TranslatePipe, FavHeart],
  template: `
    <a class="card" [routerLink]="['/', product().storeSlug, 'product', product().id]">
      <div class="image">
        @if (product().imageUrl; as image) {
          <img [src]="image" [alt]="product().name" loading="lazy" />
        } @else {
          <span class="placeholder">🛍️</span>
        }
        @if (discount() > 0) {
          <span class="discount">-{{ discount() }}%</span>
        }
        @if (!product().inStock) {
          <span class="soldout">{{ 'store.soldOut' | t }}</span>
        }
        <wo-fav-heart class="fav" kind="product" [refId]="product().id" />
      </div>
      <div class="body">
        <h4>{{ product().name | bilingual: product().nameAr }}</h4>
        <p class="store">{{ product().storeName | bilingual: product().storeNameAr }}</p>
        <p class="price">
          <strong>{{ (product().discountedPrice ?? product().price) | omr }}</strong>
          @if (product().discountedPrice !== null) {
            <s>{{ product().price | omr }}</s>
          }
        </p>
      </div>
    </a>
  `,
  styles: `
    .card {
      display: block; background: var(--wo-surface); border-radius: var(--wo-radius);
      border: 1px solid var(--wo-border); overflow: hidden; text-decoration: none; color: inherit;
      transition: box-shadow .15s, transform .15s;
    }
    .card:hover { box-shadow: var(--wo-shadow-lift); transform: translateY(-2px); }
    .image { position: relative; aspect-ratio: 1; background: var(--wo-bg); display: grid; place-items: center; }
    .image img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .placeholder { font-size: 34px; opacity: .45; }
    .discount {
      position: absolute; inset-block-start: 8px; inset-inline-start: 8px;
      background: #e11d48; color: #fff; font-size: 11px; font-weight: 700;
      padding: 3px 7px; border-radius: 99px; direction: ltr;
    }
    .soldout {
      position: absolute; inset-block-end: 8px; inset-inline-start: 8px;
      background: rgba(15, 23, 42, .8); color: #fff; font-size: 11px; font-weight: 600;
      padding: 3px 8px; border-radius: 99px;
    }
    .fav { position: absolute; inset-block-start: 8px; inset-inline-end: 8px; }
    .body { padding: 10px 12px 12px; }
    h4 { margin: 0; font-size: 14px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .store { margin: 2px 0 0; font-size: 12px; color: var(--wo-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .price { margin: 6px 0 0; font-size: 14px; display: flex; gap: 8px; align-items: baseline; }
    .price strong { color: var(--wo-primary-dark); }
    .price s { color: var(--wo-muted); font-size: 12px; }
  `,
})
export class ProductCard {
  readonly product = input.required<ProductCardDto>();

  readonly discount = computed(() => {
    const p = this.product();
    if (p.discountedPrice === null || p.price <= 0) return 0;
    return Math.round((1 - p.discountedPrice / p.price) * 100);
  });
}

@Component({
  selector: 'wo-offer-card',
  imports: [RouterLink, TranslatePipe, BilingualPipe, OmrPricePipe],
  template: `
    <a class="card" [routerLink]="['/', offer().storeSlug]">
      <div class="tag">
        @switch (offer().type) {
          @case ('Percentage') { <span>-{{ offer().discountValue }}%</span> }
          @case ('FixedAmount') { <span>-{{ offer().discountValue | omr }}</span> }
          @case ('FreeDelivery') { <span>🛵 {{ 'market.freeDelivery' | t }}</span> }
        }
      </div>
      <div class="body">
        <h4>{{ offer().title | bilingual: offer().titleAr }}</h4>
        <p class="store">
          @if (offer().storeLogoUrl; as logo) {
            <img [src]="logo" alt="" loading="lazy" />
          }
          {{ offer().storeName | bilingual: offer().storeNameAr }}
        </p>
        @if (offer().minimumOrderAmount > 0) {
          <p class="min">{{ 'market.offerMinOrder' | t: { amount: offer().minimumOrderAmount } }}</p>
        }
      </div>
    </a>
  `,
  styles: `
    .card {
      display: flex; gap: 12px; align-items: center; text-decoration: none; color: inherit;
      background: var(--wo-surface); border: 1px solid var(--wo-border);
      border-radius: var(--wo-radius); padding: 12px 14px; transition: box-shadow .15s;
    }
    .card:hover { box-shadow: var(--wo-shadow-lift); }
    .tag {
      flex: 0 0 auto; display: grid; place-items: center; min-width: 62px; height: 52px;
      border-radius: var(--wo-radius-sm); padding-inline: 8px;
      background: linear-gradient(120deg, #e11d48, #be123c); color: #fff;
      font-weight: 800; font-size: 15px; direction: ltr; text-align: center;
    }
    .body { min-width: 0; }
    h4 { margin: 0; font-size: 14.5px; font-weight: 700; }
    .store { margin: 3px 0 0; display: flex; align-items: center; gap: 6px; font-size: 12.5px; color: var(--wo-ink-soft); }
    .store img { width: 18px; height: 18px; border-radius: 5px; object-fit: cover; }
    .min { margin: 2px 0 0; font-size: 12px; color: var(--wo-muted); }
  `,
})
export class OfferCard {
  readonly offer = input.required<PublicOfferDto>();
}

/** Shimmering placeholder cards shown while a section loads. */
@Component({
  selector: 'wo-card-skeletons',
  imports: [],
  template: `
    @for (i of slots(); track i) {
      <div class="sk">
        <div class="wo-skeleton top"></div>
        <div class="wo-skeleton line"></div>
        <div class="wo-skeleton line short"></div>
      </div>
    }
  `,
  styles: `
    :host { display: contents; }
    .sk { background: var(--wo-surface); border: 1px solid var(--wo-border); border-radius: var(--wo-radius); overflow: hidden; padding-bottom: 12px; }
    .top { aspect-ratio: 16 / 8; }
    .line { height: 13px; margin: 10px 12px 0; border-radius: 6px; }
    .line.short { width: 55%; }
  `,
})
export class CardSkeletons {
  readonly count = input(4);
  readonly slots = computed(() => Array.from({ length: this.count() }, (_, i) => i));
}
