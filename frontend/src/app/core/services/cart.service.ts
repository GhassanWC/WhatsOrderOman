import { Injectable, computed, effect, signal } from '@angular/core';
import { PublicProductDto, PublicStoreDto, PublicVariantOptionDto } from '../models/api-types';

export interface CartLine {
  /** productId + sorted option ids — identifies a distinct configuration. */
  key: string;
  productId: string;
  name: string;
  nameAr: string | null;
  image: string | null;
  unitPrice: number;
  originalUnitPrice: number;
  quantity: number;
  optionIds: string[];
  optionsLabel: string | null;
  optionsLabelAr: string | null;
}

interface StoredCart {
  lines: CartLine[];
}

/**
 * Signal-based cart, persisted per store in localStorage so a customer's cart
 * survives refreshes. Prices here are for display only — the backend always
 * recomputes them from the database at checkout.
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly slug = signal<string | null>(null);
  readonly store = signal<PublicStoreDto | null>(null);
  readonly lines = signal<CartLine[]>([]);

  readonly count = computed(() => this.lines().reduce((sum, l) => sum + l.quantity, 0));
  readonly subtotal = computed(() =>
    round3(this.lines().reduce((sum, l) => sum + l.unitPrice * l.quantity, 0)));
  readonly savings = computed(() =>
    round3(this.lines().reduce((sum, l) => sum + (l.originalUnitPrice - l.unitPrice) * l.quantity, 0)));

  readonly deliveryFee = computed(() => this.store()?.deliveryFee ?? 0);
  readonly minimumOrder = computed(() => this.store()?.minimumOrderAmount ?? 0);
  readonly meetsMinimum = computed(() => this.subtotal() >= this.minimumOrder());

  constructor() {
    effect(() => {
      const slug = this.slug();
      if (!slug) return;
      const payload: StoredCart = { lines: this.lines() };
      localStorage.setItem(`wo.cart.${slug}`, JSON.stringify(payload));
    });
  }

  /** Selects the active store; loads that store's persisted cart. */
  openStore(store: PublicStoreDto): void {
    if (this.slug() !== store.slug) {
      this.slug.set(store.slug);
      this.lines.set(this.restore(store.slug));
    }
    this.store.set(store);
  }

  add(product: PublicProductDto, selectedOptions: PublicVariantOptionDto[], quantity: number): void {
    const optionIds = selectedOptions.map((o) => o.id).sort();
    const key = [product.id, ...optionIds].join('|');
    const adjustments = selectedOptions.reduce((sum, o) => sum + o.priceAdjustment, 0);
    const unitPrice = Math.max(0, round3((product.discountedPrice ?? product.price) + adjustments));
    const originalUnitPrice = Math.max(0, round3(product.price + adjustments));

    const existing = this.lines().find((l) => l.key === key);
    if (existing) {
      this.updateQuantity(key, existing.quantity + quantity);
      return;
    }

    const optionsLabel = selectedOptions.length
      ? selectedOptions.map((o) => o.name).join(' • ')
      : null;
    const optionsLabelAr = selectedOptions.length
      ? selectedOptions.map((o) => o.nameAr ?? o.name).join(' • ')
      : null;

    this.lines.update((lines) => [
      ...lines,
      {
        key,
        productId: product.id,
        name: product.name,
        nameAr: product.nameAr,
        image: product.images[0] ?? null,
        unitPrice,
        originalUnitPrice,
        quantity: Math.max(1, quantity),
        optionIds,
        optionsLabel,
        optionsLabelAr,
      },
    ]);
  }

  updateQuantity(key: string, quantity: number): void {
    if (quantity <= 0) {
      this.remove(key);
      return;
    }
    this.lines.update((lines) =>
      lines.map((l) => (l.key === key ? { ...l, quantity: Math.min(99, quantity) } : l)));
  }

  remove(key: string): void {
    this.lines.update((lines) => lines.filter((l) => l.key !== key));
  }

  clear(): void {
    this.lines.set([]);
  }

  private restore(slug: string): CartLine[] {
    try {
      const raw = localStorage.getItem(`wo.cart.${slug}`);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as StoredCart;
      return Array.isArray(parsed.lines) ? parsed.lines : [];
    } catch {
      return [];
    }
  }
}

function round3(value: number): number {
  return Math.round(value * 1000) / 1000;
}
