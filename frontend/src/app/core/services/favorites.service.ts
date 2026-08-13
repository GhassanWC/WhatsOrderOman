import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { FavoriteIdsDto } from '../models/api-types';
import { AuthService } from './auth.service';

/**
 * Session-wide favorite ids so heart buttons everywhere render instantly.
 * Optimistic toggles: the UI flips first, the API call follows, and a failure
 * rolls the flip back. Loaded once per sign-in, cleared on sign-out.
 */
@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly storeIds = signal<ReadonlySet<string>>(new Set());
  private readonly productIds = signal<ReadonlySet<string>>(new Set());
  private loadedForUser: string | null = null;

  readonly storeCount = computed(() => this.storeIds().size);
  readonly productCount = computed(() => this.productIds().size);
  readonly totalCount = computed(() => this.storeCount() + this.productCount());

  constructor() {
    effect(() => {
      const user = this.auth.user();
      if (!user) {
        this.loadedForUser = null;
        this.storeIds.set(new Set());
        this.productIds.set(new Set());
        return;
      }
      if (this.loadedForUser !== user.id) {
        this.loadedForUser = user.id;
        this.reload();
      }
    });
  }

  isStoreFavorite(storeId: string): boolean {
    return this.storeIds().has(storeId);
  }

  isProductFavorite(productId: string): boolean {
    return this.productIds().has(productId);
  }

  /** Signal reads for templates — reactive versions of the checks above. */
  readonly storeFavorite = (storeId: string) => computed(() => this.storeIds().has(storeId));
  readonly productFavorite = (productId: string) => computed(() => this.productIds().has(productId));

  /** Returns false when the user must sign in first. */
  toggleStore(storeId: string): boolean {
    if (!this.auth.isAuthenticated()) return false;
    const adding = !this.storeIds().has(storeId);
    this.mutate(this.storeIds, storeId, adding);
    const request = adding
      ? this.http.post(`/api/favorites/stores/${storeId}`, null)
      : this.http.delete(`/api/favorites/stores/${storeId}`);
    request.subscribe({ error: () => this.mutate(this.storeIds, storeId, !adding) });
    return true;
  }

  toggleProduct(productId: string): boolean {
    if (!this.auth.isAuthenticated()) return false;
    const adding = !this.productIds().has(productId);
    this.mutate(this.productIds, productId, adding);
    const request = adding
      ? this.http.post(`/api/favorites/products/${productId}`, null)
      : this.http.delete(`/api/favorites/products/${productId}`);
    request.subscribe({ error: () => this.mutate(this.productIds, productId, !adding) });
    return true;
  }

  reload(): void {
    this.http.get<FavoriteIdsDto>('/api/account/favorites/ids').subscribe({
      next: (ids) => {
        this.storeIds.set(new Set(ids.storeIds));
        this.productIds.set(new Set(ids.productIds));
      },
      error: () => undefined,
    });
  }

  private mutate(
    target: ReturnType<typeof signal<ReadonlySet<string>>>,
    id: string,
    add: boolean,
  ): void {
    target.update((current) => {
      const next = new Set(current);
      if (add) next.add(id);
      else next.delete(id);
      return next;
    });
  }
}
