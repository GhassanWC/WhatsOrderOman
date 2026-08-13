import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccountApiService } from '../../core/services/account-api.service';
import { FavoritesService } from '../../core/services/favorites.service';
import { FavoritesDto } from '../../core/models/api-types';
import { EmptyState } from '../../shared/components';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { RatingBadge } from '../marketplace/cards';

@Component({
  selector: 'wo-account-favorites',
  imports: [RouterLink, TranslatePipe, BilingualPipe, OmrPricePipe, RatingBadge, EmptyState],
  template: `
    <h1>{{ 'account.nav.favorites' | t }}</h1>

    <div class="chips">
      <button class="chip" [class.on]="tab() === 'stores'" (click)="tab.set('stores')">
        {{ 'account.favStores' | t }} ({{ favorites()?.stores?.length ?? 0 }})
      </button>
      <button class="chip" [class.on]="tab() === 'products'" (click)="tab.set('products')">
        {{ 'account.favProducts' | t }} ({{ favorites()?.products?.length ?? 0 }})
      </button>
    </div>

    @if (!favorites()) {
      <div class="wo-skeleton" style="height: 90px; margin-bottom: 10px"></div>
      <div class="wo-skeleton" style="height: 90px"></div>
    } @else if (tab() === 'stores') {
      @if (favorites()!.stores.length === 0) {
        <wo-empty-state emoji="♥" messageKey="account.noFavStores">
          <a routerLink="/stores" class="wo-btn wo-btn--primary">{{ 'market.nav.stores' | t }}</a>
        </wo-empty-state>
      } @else {
        <div class="list">
          @for (store of favorites()!.stores; track store.storeId) {
            <div class="row wo-card">
              <a class="main" [routerLink]="['/', store.slug]">
                <div class="logo">
                  @if (store.logoUrl; as logo) {
                    <img [src]="logo" alt="" loading="lazy" />
                  } @else {
                    <span>{{ store.name.charAt(0) }}</span>
                  }
                </div>
                <div class="mid">
                  <b>{{ store.name | bilingual: store.nameAr }}</b>
                  <small class="muted">
                    <wo-rating [rating]="store.rating" [count]="store.reviewsCount" />
                    @if (store.wilayat || store.governorate) { {{ store.wilayat || store.governorate }} }
                  </small>
                </div>
              </a>
              <button type="button" class="remove" (click)="removeStore(store.storeId)"
                      [attr.aria-label]="'common.remove' | t">♥</button>
            </div>
          }
        </div>
      }
    } @else {
      @if (favorites()!.products.length === 0) {
        <wo-empty-state emoji="🛍️" messageKey="account.noFavProducts">
          <a routerLink="/" class="wo-btn wo-btn--primary">{{ 'account.startShopping' | t }}</a>
        </wo-empty-state>
      } @else {
        <div class="list">
          @for (product of favorites()!.products; track product.productId) {
            <div class="row wo-card">
              <a class="main" [routerLink]="['/', product.storeSlug, 'product', product.productId]">
                <div class="logo square">
                  @if (product.imageUrl; as image) {
                    <img [src]="image" alt="" loading="lazy" />
                  } @else {
                    <span>🛍️</span>
                  }
                </div>
                <div class="mid">
                  <b>{{ product.name | bilingual: product.nameAr }}</b>
                  <small class="muted">{{ product.storeName | bilingual: product.storeNameAr }}</small>
                  <small>
                    <b class="price">{{ (product.discountedPrice ?? product.price) | omr }}</b>
                    @if (!product.inStock) { <span class="out">{{ 'store.soldOut' | t }}</span> }
                  </small>
                </div>
              </a>
              <button type="button" class="remove" (click)="removeProduct(product.productId)"
                      [attr.aria-label]="'common.remove' | t">♥</button>
            </div>
          }
        </div>
      }
    }
  `,
  styles: `
    h1 { margin: 0 0 14px; font-size: 22px; font-weight: 800; }
    .chips { display: flex; gap: 8px; margin-bottom: 14px; }
    .chip { border: 1px solid var(--wo-border); background: var(--wo-surface); cursor: pointer;
      padding: 7px 14px; border-radius: 999px; font: 600 13.5px var(--wo-font); color: var(--wo-ink-soft); }
    .chip.on { background: var(--wo-primary-soft); border-color: var(--wo-primary); color: var(--wo-primary-dark); }
    .list { display: grid; gap: 10px; }
    .row { display: flex; align-items: center; padding: 12px 14px; gap: 10px; }
    .main { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 0;
      text-decoration: none; color: inherit; }
    .logo { width: 48px; height: 48px; border-radius: 12px; overflow: hidden; flex-shrink: 0;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      display: grid; place-items: center; font-weight: 800; font-size: 18px; }
    .logo img { width: 100%; height: 100%; object-fit: cover; }
    .mid { display: grid; gap: 2px; min-width: 0;
      b { font-size: 14.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; } }
    .price { color: var(--wo-primary-dark); }
    .out { color: var(--wo-danger); font-weight: 600; margin-inline-start: 8px; }
    .remove { border: none; background: none; color: #e11d48; font-size: 22px; cursor: pointer; padding: 6px; }
  `,
})
export class AccountFavoritesPage {
  private readonly api = inject(AccountApiService);
  private readonly favoritesStore = inject(FavoritesService);

  readonly tab = signal<'stores' | 'products'>('stores');
  readonly favorites = signal<FavoritesDto | null>(null);

  constructor() {
    this.load();
  }

  removeStore(storeId: string): void {
    this.favoritesStore.toggleStore(storeId);
    this.favorites.update((f) =>
      f ? { ...f, stores: f.stores.filter((s) => s.storeId !== storeId) } : f);
  }

  removeProduct(productId: string): void {
    this.favoritesStore.toggleProduct(productId);
    this.favorites.update((f) =>
      f ? { ...f, products: f.products.filter((p) => p.productId !== productId) } : f);
  }

  private load(): void {
    this.api.favorites().subscribe({
      next: (favorites) => this.favorites.set(favorites),
      error: () => undefined,
    });
  }
}
