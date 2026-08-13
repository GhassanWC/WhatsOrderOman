import { Component, inject, input, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { StoreApiService } from '../../core/services/store-api.service';
import { CategoryDto, ProductDto, SaveProductRequest, VariantDto } from '../../core/models/api-types';
import { problemMessage } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

type VariantGroupForm = FormGroup<{
  id: import('@angular/forms').FormControl<string | null>;
  name: import('@angular/forms').FormControl<string>;
  nameAr: import('@angular/forms').FormControl<string>;
  isRequired: import('@angular/forms').FormControl<boolean>;
  options: FormArray<OptionForm>;
}>;

type OptionForm = FormGroup<{
  id: import('@angular/forms').FormControl<string | null>;
  name: import('@angular/forms').FormControl<string>;
  nameAr: import('@angular/forms').FormControl<string>;
  priceAdjustment: import('@angular/forms').FormControl<number>;
  isAvailable: import('@angular/forms').FormControl<boolean>;
}>;

@Component({
  selector: 'wo-product-editor-page',
  imports: [ReactiveFormsModule, RouterLink, MatSlideToggleModule, TranslatePipe],
  template: `
    <div class="head">
      <a routerLink="/dashboard/products" class="back">←</a>
      <h1>{{ (isNew() ? 'products.addProduct' : 'products.editProduct') | t }}</h1>
    </div>

    <form [formGroup]="form" (ngSubmit)="save()" class="layout">
      <div class="main">
        <section class="wo-card panel">
          <div class="row">
            <div class="wo-field">
              <label for="name">{{ 'products.name' | t }}</label>
              <input id="name" formControlName="name" />
            </div>
            <div class="wo-field">
              <label for="nameAr">{{ 'products.nameAr' | t }} <span class="hint">({{ 'common.optional' | t }})</span></label>
              <input id="nameAr" formControlName="nameAr" dir="rtl" />
            </div>
          </div>
          <div class="row">
            <div class="wo-field">
              <label for="desc">{{ 'products.description' | t }}</label>
              <textarea id="desc" formControlName="description" rows="3"></textarea>
            </div>
            <div class="wo-field">
              <label for="descAr">{{ 'products.descriptionAr' | t }}</label>
              <textarea id="descAr" formControlName="descriptionAr" rows="3" dir="rtl"></textarea>
            </div>
          </div>
          <div class="row row--3">
            <div class="wo-field">
              <label for="price">{{ 'products.price' | t }}</label>
              <input id="price" formControlName="price" type="number" step="0.001" min="0" inputmode="decimal" />
            </div>
            <div class="wo-field">
              <label for="dprice">{{ 'products.discountedPrice' | t }}</label>
              <input id="dprice" formControlName="discountedPrice" type="number" step="0.001" min="0" inputmode="decimal" />
              <span class="hint">{{ 'products.discountHint' | t }}</span>
            </div>
            <div class="wo-field">
              <label for="stock">{{ 'products.stock' | t }}</label>
              <input id="stock" formControlName="stockQuantity" type="number" step="1" min="0" inputmode="numeric" />
              <span class="hint">{{ 'products.stockHint' | t }}</span>
            </div>
          </div>
          <div class="row">
            <div class="wo-field">
              <label for="cat">{{ 'products.category' | t }}</label>
              <select id="cat" formControlName="categoryId">
                <option [ngValue]="null">{{ 'products.noCategory' | t }}</option>
                @for (category of categories(); track category.id) {
                  <option [ngValue]="category.id">{{ category.name }}</option>
                }
              </select>
            </div>
            <div class="toggles">
              <mat-slide-toggle formControlName="isAvailable">{{ 'products.available' | t }}</mat-slide-toggle>
              <mat-slide-toggle formControlName="isFeatured">{{ 'products.featured' | t }}</mat-slide-toggle>
            </div>
          </div>
        </section>

        <section class="wo-card panel">
          <div class="panel__head">
            <div>
              <h2>{{ 'products.variants' | t }}</h2>
              <p class="hint">{{ 'products.variantsHint' | t }}</p>
            </div>
            <button class="wo-btn wo-btn--ghost small" type="button" (click)="addVariant()">
              + {{ 'products.addVariant' | t }}
            </button>
          </div>

          @for (variant of variants.controls; track variant; let vIndex = $index) {
            <div class="variant" [formGroup]="variant">
              <div class="variant__head">
                <input class="v-name" formControlName="name" [placeholder]="'products.variantName' | t" />
                <input class="v-name" formControlName="nameAr" dir="rtl" [placeholder]="'products.variantNameAr' | t" />
                <mat-slide-toggle formControlName="isRequired" class="req">
                  {{ 'products.variantRequired' | t }}
                </mat-slide-toggle>
                <button class="icon-btn" type="button" (click)="removeVariant(vIndex)">🗑</button>
              </div>
              <div formArrayName="options" class="options">
                @for (option of variant.controls.options.controls; track option; let oIndex = $index) {
                  <div class="option" [formGroup]="option">
                    <input formControlName="name" [placeholder]="'products.optionName' | t" />
                    <input formControlName="nameAr" dir="rtl" [placeholder]="'products.optionNameAr' | t" />
                    <input formControlName="priceAdjustment" type="number" step="0.001"
                           class="adj" [placeholder]="'products.priceAdjustment' | t" inputmode="decimal" />
                    <button class="icon-btn" type="button" (click)="removeOption(vIndex, oIndex)">✕</button>
                  </div>
                }
                <button class="add-option" type="button" (click)="addOption(vIndex)">
                  + {{ 'products.addOption' | t }}
                </button>
              </div>
            </div>
          }
        </section>
      </div>

      <aside class="side">
        <section class="wo-card panel">
          <h2>{{ 'products.images' | t }}</h2>
          @if (isNew()) {
            <p class="hint">{{ 'common.save' | t }} → {{ 'products.addImage' | t }}</p>
          } @else {
            <div class="images">
              @for (image of sortedImages(); track image.id; let i = $index; let last = $last) {
                <div class="img-tile" [class.primary]="image.isPrimary">
                  <img [src]="image.url" alt="" />
                  @if (image.isPrimary) {
                    <span class="img-primary-badge">★</span>
                  }
                  <button type="button" class="img-del" (click)="deleteImage(image.id)">✕</button>
                  <div class="img-tools">
                    @if (!image.isPrimary) {
                      <button type="button" [title]="'products.setPrimary' | t"
                              (click)="setPrimaryImage(image.id)">★</button>
                    }
                    @if (i > 0) {
                      <button type="button" (click)="moveImage(i, -1)">‹</button>
                    }
                    @if (!last) {
                      <button type="button" (click)="moveImage(i, 1)">›</button>
                    }
                  </div>
                </div>
              }
              @if ((product()?.images ?? []).length < 5) {
                <label class="img-add">
                  +
                  <input type="file" accept="image/*" (change)="uploadImage($event)" hidden />
                </label>
              }
            </div>
            @if ((product()?.images ?? []).length > 1) {
              <p class="hint">{{ 'products.imageHint' | t }}</p>
            }
          }
        </section>

        @if (error()) {
          <p class="error-banner">{{ error() }}</p>
        }

        <button class="wo-btn wo-btn--primary save" type="submit" [disabled]="form.invalid || busy()">
          {{ 'common.save' | t }}
        </button>
      </aside>
    </form>
  `,
  styles: `
    .head { display: flex; align-items: center; gap: 12px; margin-bottom: 18px;
      h1 { font-size: 22px; }
      .back { font-size: 22px; color: var(--wo-ink-soft); } }
    .layout { display: grid; grid-template-columns: 1fr 280px; gap: 16px; align-items: start;
      @media (max-width: 900px) { grid-template-columns: 1fr; } }
    .main { display: grid; gap: 16px; min-width: 0; }
    .panel { padding: 20px; display: grid; gap: 16px;
      h2 { font-size: 16px; } }
    .panel__head { display: flex; justify-content: space-between; align-items: start; gap: 10px; flex-wrap: wrap; }
    .row { display: grid; gap: 14px; grid-template-columns: 1fr 1fr;
      @media (max-width: 640px) { grid-template-columns: 1fr; } }
    .row--3 { grid-template-columns: 1fr 1fr 1fr;
      @media (max-width: 640px) { grid-template-columns: 1fr; } }
    .toggles { display: flex; flex-direction: column; gap: 10px; justify-content: center; }
    .small { padding: 8px 16px; font-size: 13.5px; }
    .variant { border: 1.5px solid var(--wo-border); border-radius: 12px; padding: 14px; display: grid; gap: 10px; }
    .variant__head { display: flex; gap: 8px; align-items: center; flex-wrap: wrap;
      .v-name { flex: 1; min-width: 130px; } }
    .req { font-size: 13px; }
    input, select, textarea { border: 1.5px solid var(--wo-border); border-radius: 10px; padding: 9px 12px;
      font: 500 14px var(--wo-font); outline: none; background: var(--wo-surface); width: 100%;
      &:focus { border-color: var(--wo-primary); } }
    .options { display: grid; gap: 8px; }
    .option { display: grid; grid-template-columns: 1fr 1fr 110px auto; gap: 8px; align-items: center;
      @media (max-width: 640px) { grid-template-columns: 1fr 1fr; .adj { grid-column: 1; } } }
    .add-option { justify-self: start; border: none; background: none; color: var(--wo-primary);
      font: 600 13.5px var(--wo-font); cursor: pointer; padding: 4px 0; }
    .icon-btn { border: none; background: none; cursor: pointer; opacity: .55; font-size: 15px; &:hover { opacity: 1; } }
    .side { display: grid; gap: 14px; position: sticky; top: 80px; }
    .images { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
    .img-tile { position: relative; aspect-ratio: 1; border-radius: 10px; overflow: hidden;
      img { width: 100%; height: 100%; object-fit: cover; }
      .img-del { position: absolute; top: 4px; inset-inline-end: 4px; border: none; background: rgba(0,0,0,.55);
        color: #fff; width: 22px; height: 22px; border-radius: 50%; cursor: pointer; font-size: 11px; } }
    .img-tile.primary { outline: 2px solid var(--wo-primary); outline-offset: -2px; }
    .img-primary-badge { position: absolute; top: 4px; inset-inline-start: 4px; color: #fbbf24;
      text-shadow: 0 1px 2px rgba(0,0,0,.6); font-size: 14px; }
    .img-tools { position: absolute; inset-inline: 0; bottom: 0; display: flex; justify-content: center;
      gap: 4px; padding: 3px; background: linear-gradient(transparent, rgba(0,0,0,.55));
      button { border: none; background: rgba(255,255,255,.9); border-radius: 6px; width: 22px; height: 20px;
        cursor: pointer; font-size: 12px; line-height: 1; display: grid; place-items: center; } }
    .img-add { aspect-ratio: 1; border: 2px dashed var(--wo-border); border-radius: 10px; display: grid;
      place-items: center; font-size: 24px; color: var(--wo-muted); cursor: pointer;
      &:hover { border-color: var(--wo-primary); color: var(--wo-primary); } }
    .save { width: 100%; }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; }
  `,
})
export class ProductEditorPage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(StoreApiService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);

  /** Route param (present when editing). */
  readonly id = input<string | undefined>();

  readonly product = signal<ProductDto | null>(null);
  readonly categories = signal<CategoryDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly isNew = () => !this.id();

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
    nameAr: [''],
    description: ['', Validators.maxLength(1000)],
    descriptionAr: ['', Validators.maxLength(1000)],
    price: [0, [Validators.required, Validators.min(0)]],
    discountedPrice: [null as number | null, Validators.min(0)],
    stockQuantity: [null as number | null, Validators.min(0)],
    categoryId: [null as string | null],
    isAvailable: [true],
    isFeatured: [false],
    variants: this.fb.array<VariantGroupForm>([]),
  });

  get variants(): FormArray<VariantGroupForm> {
    return this.form.controls.variants;
  }

  constructor() {
    this.api.getCategories().subscribe((categories) => this.categories.set(categories));
    // input() is not resolved in the constructor — defer one tick.
    queueMicrotask(() => {
      const id = this.id();
      if (id) {
        this.api.getProduct(id).subscribe((product) => this.populate(product));
      }
    });
  }

  private populate(product: ProductDto): void {
    this.product.set(product);
    this.form.patchValue({
      name: product.name,
      nameAr: product.nameAr ?? '',
      description: product.description ?? '',
      descriptionAr: product.descriptionAr ?? '',
      price: product.price,
      discountedPrice: product.discountedPrice,
      stockQuantity: product.stockQuantity,
      categoryId: product.categoryId,
      isAvailable: product.isAvailable,
      isFeatured: product.isFeatured,
    });
    this.variants.clear();
    for (const variant of product.variants) {
      this.variants.push(this.buildVariant(variant));
    }
  }

  private buildVariant(variant?: VariantDto): VariantGroupForm {
    const group = this.fb.nonNullable.group({
      id: this.fb.control<string | null>(variant?.id ?? null),
      name: this.fb.nonNullable.control(variant?.name ?? '', [Validators.required, Validators.maxLength(60)]),
      nameAr: this.fb.nonNullable.control(variant?.nameAr ?? ''),
      isRequired: this.fb.nonNullable.control(variant?.isRequired ?? true),
      options: this.fb.array<OptionForm>([]),
    });
    for (const option of variant?.options ?? []) {
      group.controls.options.push(this.buildOption(option));
    }
    if (group.controls.options.length === 0) {
      group.controls.options.push(this.buildOption());
    }
    return group;
  }

  private buildOption(option?: VariantDto['options'][number]): OptionForm {
    return this.fb.nonNullable.group({
      id: this.fb.control<string | null>(option?.id ?? null),
      name: this.fb.nonNullable.control(option?.name ?? '', [Validators.required, Validators.maxLength(60)]),
      nameAr: this.fb.nonNullable.control(option?.nameAr ?? ''),
      priceAdjustment: this.fb.nonNullable.control(option?.priceAdjustment ?? 0),
      isAvailable: this.fb.nonNullable.control(option?.isAvailable ?? true),
    });
  }

  addVariant(): void {
    this.variants.push(this.buildVariant());
  }

  removeVariant(index: number): void {
    this.variants.removeAt(index);
  }

  addOption(variantIndex: number): void {
    this.variants.at(variantIndex).controls.options.push(this.buildOption());
  }

  removeOption(variantIndex: number, optionIndex: number): void {
    this.variants.at(variantIndex).controls.options.removeAt(optionIndex);
  }

  save(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    const request: SaveProductRequest = {
      categoryId: value.categoryId,
      name: value.name,
      nameAr: value.nameAr || null,
      description: value.description || null,
      descriptionAr: value.descriptionAr || null,
      price: Number(value.price),
      discountedPrice: value.discountedPrice !== null && `${value.discountedPrice}` !== ''
        ? Number(value.discountedPrice) : null,
      stockQuantity: value.stockQuantity !== null && `${value.stockQuantity}` !== ''
        ? Number(value.stockQuantity) : null,
      isAvailable: value.isAvailable,
      isFeatured: value.isFeatured,
      variants: value.variants.map((variant, vIndex) => ({
        id: variant.id,
        name: variant.name,
        nameAr: variant.nameAr || null,
        isRequired: variant.isRequired,
        sortOrder: vIndex,
        options: variant.options.map((option, oIndex) => ({
          id: option.id,
          name: option.name,
          nameAr: option.nameAr || null,
          priceAdjustment: Number(option.priceAdjustment) || 0,
          isAvailable: option.isAvailable,
          sortOrder: oIndex,
        })),
      })),
    };

    const call = this.isNew()
      ? this.api.createProduct(request)
      : this.api.updateProduct(this.id()!, request);

    call.subscribe({
      next: (product) => {
        this.snackBar.open(this.i18n.t('products.saved'), undefined, { duration: 2000 });
        if (this.isNew()) {
          void this.router.navigate(['/dashboard/products', product.id, 'edit'], { replaceUrl: true });
          this.product.set(product);
          this.busy.set(false);
        } else {
          void this.router.navigate(['/dashboard/products']);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err, this.i18n.t('common.error')));
      },
    });
  }

  uploadImage(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    const id = this.id();
    if (!file || !id) return;
    this.api.addProductImage(id, file).subscribe({
      next: (product) => this.product.set(product),
      error: (err) => this.error.set(problemMessage(err, this.i18n.t('common.error'))),
    });
  }

  deleteImage(imageId: string): void {
    const id = this.id();
    if (!id) return;
    this.api.deleteProductImage(id, imageId).subscribe((product) => this.product.set(product));
  }

  sortedImages() {
    return [...(this.product()?.images ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
  }

  setPrimaryImage(imageId: string): void {
    const id = this.id();
    if (!id) return;
    this.api.setPrimaryProductImage(id, imageId).subscribe((product) => this.product.set(product));
  }

  moveImage(index: number, delta: number): void {
    const id = this.id();
    if (!id) return;
    const ids = this.sortedImages().map((image) => image.id);
    const target = index + delta;
    if (target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    this.api.reorderProductImages(id, ids).subscribe((product) => this.product.set(product));
  }
}
