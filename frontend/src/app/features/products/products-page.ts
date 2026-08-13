import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { StoreApiService } from '../../core/services/store-api.service';
import { CategoryDto, ProductDto } from '../../core/models/api-types';
import { ConfirmDialog, EmptyState } from '../../shared/components';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';
import { CategoryManagerDialog } from './category-manager-dialog';

@Component({
  selector: 'wo-products-page',
  imports: [RouterLink, FormsModule, MatSlideToggleModule, EmptyState, BilingualPipe, OmrPricePipe, TranslatePipe],
  template: `
    <div class="head">
      <h1>{{ 'products.title' | t }}</h1>
      <span class="spacer"></span>
      <button class="wo-btn wo-btn--ghost" type="button" (click)="openCategories()">
        {{ 'products.manageCategories' | t }}
      </button>
      <a class="wo-btn wo-btn--primary" routerLink="/dashboard/products/new">
        + {{ 'products.addProduct' | t }}
      </a>
    </div>

    <div class="filters">
      <input class="search" type="search" [placeholder]="'products.searchPlaceholder' | t"
             [ngModel]="search()" (ngModelChange)="onSearch($event)" />
      <select class="cat" [ngModel]="categoryId()" (ngModelChange)="onCategory($event)">
        <option value="">{{ 'products.allCategories' | t }}</option>
        @for (category of categories(); track category.id) {
          <option [value]="category.id">{{ category.name | bilingual: category.nameAr }}</option>
        }
      </select>
    </div>

    @if (loading()) {
      <div class="list">
        @for (i of [1, 2, 3, 4]; track i) {
          <div class="wo-skeleton" style="height: 84px"></div>
        }
      </div>
    } @else if (products().length === 0) {
      <wo-empty-state emoji="🛍️" messageKey="products.empty">
        <a class="wo-btn wo-btn--primary" routerLink="/dashboard/products/new">+ {{ 'products.addProduct' | t }}</a>
      </wo-empty-state>
    } @else {
      <div class="list">
        @for (product of products(); track product.id) {
          <div class="item wo-card" [class.item--off]="!product.isAvailable">
            <div class="thumb">
              @if (product.images.length > 0) {
                <img [src]="product.images[0].url" [alt]="product.name" loading="lazy" />
              } @else {
                <span>{{ product.name.charAt(0) }}</span>
              }
            </div>
            <div class="info">
              <div class="name-row">
                <a class="name" [routerLink]="['/dashboard/products', product.id, 'edit']">
                  {{ product.name | bilingual: product.nameAr }}
                </a>
                @if (product.isFeatured) { <span class="star" title="Featured">★</span> }
              </div>
              <div class="meta muted">
                @if (product.categoryName) { <span>{{ product.categoryName }}</span> }
                @if (product.variants.length > 0) { <span>· {{ product.variants.length }} ⚙</span> }
                <span>·
                  @if (product.stockQuantity === null) { {{ 'products.unlimited' | t }} }
                  @else if (product.stockQuantity === 0) { <b class="out">{{ 'products.outOfStock' | t }}</b> }
                  @else { {{ 'products.leftInStock' | t: { count: product.stockQuantity } }} }
                </span>
              </div>
            </div>
            <div class="price">
              @if (product.discountedPrice !== null) {
                <span class="now">{{ product.discountedPrice | omr }}</span>
                <s class="was muted">{{ product.price | omr }}</s>
              } @else {
                <span class="now">{{ product.price | omr }}</span>
              }
            </div>
            <mat-slide-toggle [checked]="product.isAvailable"
                              (change)="toggleAvailability(product, $event.checked)"
                              [attr.aria-label]="'products.available' | t" />
            <button class="delete" type="button" (click)="remove(product)" [title]="'common.delete' | t">🗑</button>
          </div>
        }
      </div>
    }
  `,
  styles: `
    .head { display: flex; align-items: center; gap: 10px; margin-bottom: 18px; flex-wrap: wrap;
      h1 { font-size: 22px; } }
    .filters { display: flex; gap: 10px; margin-bottom: 16px; flex-wrap: wrap; }
    .search { flex: 1; min-width: 200px; border: 1.5px solid var(--wo-border); border-radius: 999px;
      padding: 10px 18px; font: 500 14.5px var(--wo-font); outline: none; background: var(--wo-surface);
      &:focus { border-color: var(--wo-primary); } }
    .cat { border: 1.5px solid var(--wo-border); border-radius: 999px; padding: 10px 16px;
      font: 500 14px var(--wo-font); background: var(--wo-surface); outline: none; }
    .list { display: grid; gap: 10px; }
    .item { display: flex; align-items: center; gap: 14px; padding: 12px 16px; }
    .item--off { opacity: .55; }
    .thumb { width: 56px; height: 56px; border-radius: 12px; background: var(--wo-primary-soft); overflow: hidden;
      display: grid; place-items: center; font: 800 22px var(--wo-font); color: var(--wo-primary-dark); flex-shrink: 0;
      img { width: 100%; height: 100%; object-fit: cover; } }
    .info { flex: 1; min-width: 0; }
    .name-row { display: flex; align-items: center; gap: 6px; }
    .name { font-weight: 700; color: var(--wo-ink); overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      &:hover { color: var(--wo-primary); } }
    .star { color: #f59e0b; }
    .meta { font-size: 12.5px; display: flex; gap: 5px; flex-wrap: wrap; .out { color: var(--wo-danger); } }
    .price { text-align: end; display: grid; .now { font-weight: 800; } .was { font-size: 12px; } }
    .delete { border: none; background: none; cursor: pointer; font-size: 16px; opacity: .5; &:hover { opacity: 1; } }
    @media (max-width: 620px) {
      .item { flex-wrap: wrap; }
      .price { order: 5; }
    }
  `,
})
export class ProductsPage {
  private readonly api = inject(StoreApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);

  readonly products = signal<ProductDto[]>([]);
  readonly categories = signal<CategoryDto[]>([]);
  readonly loading = signal(true);
  readonly search = signal('');
  readonly categoryId = signal('');

  private readonly search$ = new Subject<string>();

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.load());
    this.load();
    this.loadCategories();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.search$.next(value);
  }

  onCategory(value: string): void {
    this.categoryId.set(value);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.getProducts(this.search() || undefined, this.categoryId() || undefined).subscribe({
      next: (page) => {
        this.products.set(page.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private loadCategories(): void {
    this.api.getCategories().subscribe((categories) => this.categories.set(categories));
  }

  toggleAvailability(product: ProductDto, isAvailable: boolean): void {
    this.api.setProductAvailability(product.id, isAvailable).subscribe({
      next: (updated) =>
        this.products.update((list) => list.map((p) => (p.id === updated.id ? updated : p))),
      error: () => this.load(),
    });
  }

  remove(product: ProductDto): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          titleKey: 'products.deleteTitle',
          messageKey: 'products.deleteMessage',
          messageParams: { name: product.name },
          confirmKey: 'common.delete',
          danger: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.api.deleteProduct(product.id).subscribe(() => {
          this.snackBar.open(this.i18n.t('products.deleted'), undefined, { duration: 2000 });
          this.load();
        });
      });
  }

  openCategories(): void {
    this.dialog
      .open(CategoryManagerDialog, { width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe(() => {
        this.loadCategories();
        this.load();
      });
  }
}
