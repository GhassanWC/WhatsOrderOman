import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CartService } from '../../core/services/cart.service';
import { PublicStoreService } from '../../core/services/public-store.service';
import { MarketplaceApiService } from '../../core/services/marketplace-api.service';
import {
  ProductCardDto,
  PublicProductDto,
  PublicStoreDto,
  PublicVariantOptionDto,
} from '../../core/models/api-types';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';
import { shareOrCopy } from '../../shared/share';
import { FavHeart, ProductCard } from '../marketplace/cards';

/**
 * Routed, shareable product page: /{storeSlug}/product/{productId}.
 * Image gallery, variant selection, quantity, add-to-cart, and related products.
 */
@Component({
  selector: 'wo-product-page',
  imports: [RouterLink, BilingualPipe, OmrPricePipe, TranslatePipe, FavHeart, ProductCard],
  template: `
    @if (notFound()) {
      <div class="not-found">
        <div class="emoji">🔍</div>
        <h1>{{ 'product.notFound' | t }}</h1>
        @if (store(); as s) {
          <a [routerLink]="['/', s.slug]" class="wo-btn wo-btn--primary">{{ 'product.backToStore' | t }}</a>
        }
      </div>
    } @else if (product(); as p) {
      <div class="container page">
        <nav class="crumbs">
          <a [routerLink]="['/', slug()]">← {{ storeName() }}</a>
        </nav>

        <div class="layout">
          <!-- ── Gallery ─────────────────────────────────────────────── -->
          <div class="gallery">
            <div class="main-img">
              @if (activeImage(); as image) {
                <img [src]="image" [alt]="p.name" />
              } @else {
                <span class="ph">🛍️</span>
              }
              @if (p.discountedPrice !== null) {
                <span class="sale">-{{ discountPercent() }}%</span>
              }
              <wo-fav-heart class="fav" kind="product" [refId]="p.id" />
            </div>
            @if (p.images.length > 1) {
              <div class="thumbs">
                @for (image of p.images; track image; let i = $index) {
                  <button type="button" class="thumb" [class.on]="activeIndex() === i"
                          (click)="activeIndex.set(i)">
                    <img [src]="image" alt="" loading="lazy" />
                  </button>
                }
              </div>
            }
          </div>

          <!-- ── Details ─────────────────────────────────────────────── -->
          <div class="details">
            <h1>{{ p.name | bilingual: p.nameAr }}</h1>
            <div class="price-row">
              <span class="price">{{ (p.discountedPrice ?? p.price) | omr }}</span>
              @if (p.discountedPrice !== null) { <s>{{ p.price | omr }}</s> }
              @if (!p.inStock) { <span class="soldout">{{ 'store.soldOut' | t }}</span> }
            </div>
            @if (p.description || p.descriptionAr) {
              <p class="desc">{{ p.description | bilingual: p.descriptionAr }}</p>
            }

            @for (variant of p.variants; track variant.id) {
              <div class="variant">
                <h3>
                  {{ 'store.chooseOption' | t: { name: (variant.name | bilingual: variant.nameAr) } }}
                  @if (variant.isRequired) { <span class="req">*</span> }
                </h3>
                <div class="opts">
                  @for (option of variant.options; track option.id) {
                    <button type="button" class="opt" [class.opt--on]="isSelected(variant.id, option.id)"
                            (click)="select(variant.id, option)">
                      {{ option.name | bilingual: option.nameAr }}
                      @if (option.priceAdjustment !== 0) {
                        <small>{{ option.priceAdjustment > 0 ? '+' : '' }}{{ option.priceAdjustment | omr }}</small>
                      }
                    </button>
                  }
                </div>
              </div>
            }

            <div class="qty-row">
              <span class="label">{{ 'store.quantity' | t }}</span>
              <div class="stepper">
                <button type="button" (click)="decrement()" [disabled]="quantity() <= 1">−</button>
                <span>{{ quantity() }}</span>
                <button type="button" (click)="increment()">+</button>
              </div>
            </div>

            <div class="cta-row">
              <button type="button" class="wo-btn wo-btn--primary cta" (click)="addToCart()"
                      [disabled]="!canAdd()">
                @if (!p.inStock) {
                  {{ 'store.soldOut' | t }}
                } @else {
                  {{ 'store.addToCart' | t }} · {{ totalPrice() | omr }}
                }
              </button>
              <button type="button" class="wo-btn wo-btn--ghost" (click)="share(p)">↗</button>
            </div>

            @if (store(); as s) {
              <a class="store-strip" [routerLink]="['/', s.slug]">
                @if (s.logoUrl; as logo) {
                  <img [src]="logo" alt="" />
                } @else {
                  <span class="strip-initial">{{ s.name.charAt(0) }}</span>
                }
                <span class="strip-name">{{ s.name | bilingual: s.nameAr }}</span>
                <span class="strip-cta">{{ 'product.visitStore' | t }} →</span>
              </a>
            }
          </div>
        </div>

        <!-- ── Related products ────────────────────────────────────────── -->
        @if (related().length > 0) {
          <section class="related">
            <h2>{{ 'product.related' | t }}</h2>
            <div class="grid">
              @for (item of related(); track item.id) {
                <wo-product-card [product]="item" />
              }
            </div>
          </section>
        }

        <!-- ── Sticky cart bar ─────────────────────────────────────────── -->
        @if (cart.count() > 0) {
          <a class="cart-bar" [routerLink]="['/', slug(), 'cart']">
            <span class="count">{{ cart.count() }}</span>
            <span>{{ 'store.viewCart' | t }}</span>
            <span class="spacer"></span>
            <b>{{ cart.subtotal() | omr }}</b>
          </a>
        }
      </div>
    } @else {
      <div class="container page">
        <div class="layout">
          <div class="wo-skeleton" style="aspect-ratio: 1"></div>
          <div>
            <div class="wo-skeleton" style="height: 34px; margin-bottom: 14px"></div>
            <div class="wo-skeleton" style="height: 20px; width: 40%; margin-bottom: 22px"></div>
            <div class="wo-skeleton" style="height: 120px"></div>
          </div>
        </div>
      </div>
    }
  `,
  styles: `
    .page { padding-block: 18px 90px; }
    .not-found { min-height: 60dvh; display: grid; place-content: center; text-align: center; gap: 12px;
      .emoji { font-size: 50px; } }
    .crumbs { margin-bottom: 14px;
      a { text-decoration: none; color: var(--wo-ink-soft); font-weight: 600; font-size: 14px; } }
    .layout { display: grid; grid-template-columns: minmax(0, 460px) 1fr; gap: 28px; align-items: start; }
    .gallery { display: grid; gap: 10px; }
    .main-img { position: relative; aspect-ratio: 1; border-radius: var(--wo-radius); overflow: hidden;
      background: var(--wo-primary-soft); display: grid; place-items: center;
      img { width: 100%; height: 100%; object-fit: cover; }
      .ph { font-size: 64px; opacity: .4; } }
    .sale { position: absolute; top: 12px; inset-inline-start: 12px; background: var(--wo-danger); color: #fff;
      border-radius: 999px; padding: 4px 12px; font: 700 13px var(--wo-font); direction: ltr; }
    .fav { position: absolute; top: 12px; inset-inline-end: 12px; }
    .thumbs { display: flex; gap: 8px; overflow-x: auto; }
    .thumb { flex: 0 0 64px; aspect-ratio: 1; border-radius: 10px; overflow: hidden; padding: 0;
      border: 2px solid transparent; cursor: pointer; background: var(--wo-bg);
      img { width: 100%; height: 100%; object-fit: cover; display: block; }
      &.on { border-color: var(--wo-primary); } }
    .details { display: grid; gap: 14px; align-content: start;
      h1 { margin: 0; font-size: 26px; font-weight: 800; } }
    .price-row { display: flex; align-items: baseline; gap: 10px;
      .price { font-size: 22px; font-weight: 800; color: var(--wo-primary-dark); }
      s { color: var(--wo-muted); }
      .soldout { background: var(--wo-ink); color: #fff; font-size: 12px; font-weight: 600;
        border-radius: 999px; padding: 3px 10px; } }
    .desc { margin: 0; color: var(--wo-ink-soft); font-size: 14.5px; line-height: 1.55; }
    .variant { display: grid; gap: 8px;
      h3 { margin: 0; font-size: 14px; color: var(--wo-ink-soft); font-weight: 600; }
      .req { color: var(--wo-danger); } }
    .opts { display: flex; gap: 8px; flex-wrap: wrap; }
    .opt { border: 1.5px solid var(--wo-border); background: var(--wo-surface); border-radius: 999px;
      padding: 8px 16px; font: 600 13.5px var(--wo-font); cursor: pointer; display: inline-flex; gap: 6px;
      align-items: center;
      small { color: var(--wo-ink-soft); font-weight: 500; }
      &.opt--on { border-color: var(--wo-primary); background: var(--wo-primary-soft); color: var(--wo-primary-dark);
        small { color: var(--wo-primary-dark); } } }
    .qty-row { display: flex; align-items: center; justify-content: space-between; max-width: 300px;
      .label { font-weight: 600; font-size: 14px; color: var(--wo-ink-soft); } }
    .stepper { display: flex; align-items: center; gap: 4px; border: 1.5px solid var(--wo-border);
      border-radius: 999px; padding: 4px;
      button { width: 32px; height: 32px; border: none; border-radius: 50%; background: var(--wo-bg);
        font-size: 17px; cursor: pointer; &:disabled { opacity: .4; } &:hover:not(:disabled) { background: var(--wo-primary-soft); } }
      span { min-width: 34px; text-align: center; font-weight: 700; } }
    .cta-row { display: flex; gap: 10px;
      .cta { flex: 1; } }
    .store-strip { display: flex; align-items: center; gap: 10px; text-decoration: none; color: inherit;
      background: var(--wo-surface); border: 1px solid var(--wo-border); border-radius: var(--wo-radius-sm);
      padding: 10px 14px;
      img { width: 34px; height: 34px; border-radius: 9px; object-fit: cover; }
      .strip-initial { width: 34px; height: 34px; border-radius: 9px; background: var(--wo-primary-soft);
        color: var(--wo-primary-dark); display: grid; place-items: center; font-weight: 800; }
      .strip-name { font-weight: 700; font-size: 14px; }
      .strip-cta { margin-inline-start: auto; font-size: 13px; color: var(--wo-primary-dark); font-weight: 600; } }
    .related { margin-top: 34px;
      h2 { font-size: 18px; font-weight: 800; margin-bottom: 12px; } }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 14px; }
    .cart-bar { position: fixed; bottom: 14px; inset-inline: 14px; max-width: 560px; margin-inline: auto;
      background: var(--wo-ink); color: #fff; border-radius: 999px; padding: 14px 22px; display: flex;
      align-items: center; gap: 12px; font: 600 15px var(--wo-font); box-shadow: var(--wo-shadow-lift); z-index: 30;
      .count { background: var(--wo-accent); color: #073b27; width: 26px; height: 26px; border-radius: 50%;
        display: grid; place-items: center; font-size: 13px; font-weight: 800; } }
    @media (max-width: 760px) {
      .layout { grid-template-columns: 1fr; }
      .cart-bar { bottom: 76px; }
    }
  `,
})
export class ProductPage {
  private readonly api = inject(PublicStoreService);
  private readonly marketplace = inject(MarketplaceApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);
  readonly cart = inject(CartService);

  /** Bound from route params. */
  readonly slug = input.required<string>();
  readonly productId = input.required<string>();

  readonly store = signal<PublicStoreDto | null>(null);
  readonly product = signal<PublicProductDto | null>(null);
  readonly related = signal<ProductCardDto[]>([]);
  readonly notFound = signal(false);
  readonly quantity = signal(1);
  readonly activeIndex = signal(0);
  readonly selected = signal<Map<string, PublicVariantOptionDto>>(new Map());

  readonly storeName = computed(() => {
    const s = this.store();
    return s ? (this.i18n.lang() === 'ar' && s.nameAr ? s.nameAr : s.name) : '';
  });

  readonly activeImage = computed(() => {
    const p = this.product();
    if (!p || p.images.length === 0) return null;
    return p.images[Math.min(this.activeIndex(), p.images.length - 1)];
  });

  readonly unitPrice = computed(() => {
    const p = this.product();
    if (!p) return 0;
    const adjustments = [...this.selected().values()].reduce((sum, o) => sum + o.priceAdjustment, 0);
    return Math.max(0, (p.discountedPrice ?? p.price) + adjustments);
  });

  readonly totalPrice = computed(() => this.unitPrice() * this.quantity());

  readonly canAdd = computed(() => {
    const p = this.product();
    const s = this.store();
    if (!p || !s || !p.inStock || !s.acceptingOrders) return false;
    return p.variants
      .filter((v) => v.isRequired && v.options.length > 0)
      .every((v) => this.selected().has(v.id));
  });

  readonly discountPercent = computed(() => {
    const p = this.product();
    if (!p || p.discountedPrice === null || p.price === 0) return 0;
    return Math.round((1 - p.discountedPrice / p.price) * 100);
  });

  private loadedKey: string | null = null;

  constructor() {
    effect(() => {
      const key = `${this.slug()}/${this.productId()}`;
      if (key !== this.loadedKey) {
        this.loadedKey = key;
        this.load();
      }
    });
  }

  private load(): void {
    const slug = this.slug();
    const productId = this.productId();
    this.product.set(null);
    this.notFound.set(false);
    this.selected.set(new Map());
    this.quantity.set(1);
    this.activeIndex.set(0);

    this.api.getStore(slug).subscribe({
      next: (store) => {
        this.store.set(store);
        this.cart.openStore(store);
      },
      error: () => this.notFound.set(true),
    });
    this.api.getProduct(slug, productId).subscribe({
      next: (product) => this.product.set(product),
      error: () => this.notFound.set(true),
    });
    this.marketplace.relatedProducts(productId, 8).subscribe({
      next: (related) => this.related.set(related),
      error: () => undefined,
    });
  }

  isSelected(variantId: string, optionId: string): boolean {
    return this.selected().get(variantId)?.id === optionId;
  }

  select(variantId: string, option: PublicVariantOptionDto): void {
    this.selected.update((map) => {
      const next = new Map(map);
      if (next.get(variantId)?.id === option.id) next.delete(variantId);
      else next.set(variantId, option);
      return next;
    });
  }

  increment(): void {
    this.quantity.update((q) => Math.min(99, q + 1));
  }

  decrement(): void {
    this.quantity.update((q) => Math.max(1, q - 1));
  }

  addToCart(): void {
    const product = this.product();
    const store = this.store();
    if (!product || !store || !this.canAdd()) return;
    this.cart.add(product, [...this.selected().values()], this.quantity());
    this.marketplace.track({
      eventType: 'AddedToCart', storeId: store.id, productId: product.id, categoryId: null,
    });
    this.snackBar.open(this.i18n.t('store.addedToCart'), undefined, { duration: 1500 });
  }

  async share(product: PublicProductDto): Promise<void> {
    const result = await shareOrCopy(product.name, `/${this.slug()}/product/${product.id}`);
    if (result === 'copied') {
      this.snackBar.open(this.i18n.t('common.linkCopied'), undefined, { duration: 2000 });
    }
  }
}
