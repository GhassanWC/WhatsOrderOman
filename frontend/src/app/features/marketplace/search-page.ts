import { Component, effect, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MarketplaceApiService } from '../../core/services/marketplace-api.service';
import { SearchResultsDto, SearchSuggestionsDto } from '../../core/models/api-types';
import { EmptyState } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { CardSkeletons, ProductCard, StoreCard } from './cards';

/**
 * Unified search: /search?q=burger. Typing shows live suggestions; an empty box
 * shows the buyer's recent searches and marketplace-popular ones.
 */
@Component({
  selector: 'wo-search-page',
  imports: [TranslatePipe, StoreCard, ProductCard, CardSkeletons, EmptyState],
  template: `
    <div class="container page">
      <form class="box" (submit)="submit($event)">
        <input
          #searchInput
          type="search"
          [value]="term()"
          (input)="onType($any($event.target).value)"
          [placeholder]="'market.searchPlaceholder' | t"
          autofocus
        />
        <button type="submit" class="wo-btn wo-btn--primary">{{ 'market.searchAction' | t }}</button>
      </form>

      @if (!submitted()) {
        @if (suggest(); as s) {
          <div class="suggest">
            @if (s.suggestions.length > 0) {
              <h3>{{ 'market.suggestions' | t }}</h3>
              <div class="chips">
                @for (item of s.suggestions; track item) {
                  <button class="chip" (click)="runSearch(item)">{{ item }}</button>
                }
              </div>
            }
            @if (s.recent.length > 0) {
              <h3>{{ 'market.recentSearches' | t }}</h3>
              <div class="chips">
                @for (item of s.recent; track item) {
                  <button class="chip" (click)="runSearch(item)">🕘 {{ item }}</button>
                }
              </div>
            }
            @if (s.popular.length > 0) {
              <h3>{{ 'market.popularSearches' | t }}</h3>
              <div class="chips">
                @for (item of s.popular; track item) {
                  <button class="chip" (click)="runSearch(item)">🔥 {{ item }}</button>
                }
              </div>
            }
          </div>
        }
      } @else if (loading()) {
        <div class="grid products"><wo-card-skeletons [count]="6" /></div>
      } @else if (results(); as r) {
        @if (r.stores.length === 0 && r.products.length === 0) {
          <wo-empty-state emoji="🔍" messageKey="market.noResults" />
        } @else {
          @if (r.categories.length > 0) {
            <div class="chips categories">
              @for (category of r.categories; track category.name) {
                <button class="chip" (click)="runSearch(category.name)">
                  {{ category.name }} <span class="chip-count">{{ category.storeCount }}</span>
                </button>
              }
            </div>
          }
          @if (r.stores.length > 0) {
            <section>
              <h2>{{ 'market.storeResults' | t: { count: r.storesTotal } }}</h2>
              <div class="grid stores">
                @for (store of r.stores; track store.id) {
                  <wo-store-card [store]="store" />
                }
              </div>
            </section>
          }
          @if (r.products.length > 0) {
            <section>
              <h2>{{ 'market.productResults' | t: { count: r.productsTotal } }}</h2>
              <div class="grid products">
                @for (product of r.products; track product.id) {
                  <wo-product-card [product]="product" />
                }
              </div>
            </section>
          }
        }
      }
    </div>
  `,
  styles: `
    .page { padding-block: 22px 46px; display: grid; gap: 20px; align-content: start; }
    .box {
      display: flex; gap: 8px; background: var(--wo-surface); padding: 6px;
      border: 1px solid var(--wo-border); border-radius: 99px;
    }
    .box input { flex: 1; border: none; outline: none; font: inherit; padding: 8px 16px; min-width: 0; background: transparent; }
    .box .wo-btn { border-radius: 99px; }
    .suggest h3 { margin: 14px 0 8px; font-size: 14px; color: var(--wo-ink-soft); }
    .chips { display: flex; gap: 8px; flex-wrap: wrap; }
    .chip {
      border: 1px solid var(--wo-border); background: var(--wo-surface); cursor: pointer;
      padding: 7px 14px; border-radius: 99px; font: inherit; font-size: 13.5px;
    }
    .chip:hover { border-color: var(--wo-primary); }
    .chip-count { color: var(--wo-muted); font-size: 12px; }
    section h2 { font-size: 17px; font-weight: 800; margin: 4px 0 12px; }
    .grid.stores { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 14px; }
    .grid.products { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 14px; }
  `,
})
export class SearchPage {
  private readonly api = inject(MarketplaceApiService);
  private readonly router = inject(Router);

  /** Bound from ?q= by withComponentInputBinding. */
  readonly q = input<string>();

  readonly term = signal('');
  readonly submitted = signal(false);
  readonly loading = signal(false);
  readonly results = signal<SearchResultsDto | null>(null);
  readonly suggest = signal<SearchSuggestionsDto | null>(null);

  private readonly type$ = new Subject<string>();

  constructor() {
    // Live suggestions while typing (also fetches recent/popular for empty input).
    this.type$
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((value) => this.api.suggest(value)),
        takeUntilDestroyed(),
      )
      .subscribe((s) => this.suggest.set(s));
    this.type$.next('');

    // React to ?q= changes (header search, suggestion chips).
    effect(() => {
      const q = this.q()?.trim();
      if (q) {
        this.term.set(q);
        this.execute(q);
      }
    });
  }

  onType(value: string): void {
    this.term.set(value);
    this.submitted.set(false);
    this.type$.next(value.trim());
  }

  submit(event: Event): void {
    event.preventDefault();
    const q = this.term().trim();
    if (q) this.runSearch(q);
  }

  runSearch(q: string): void {
    this.term.set(q);
    void this.router.navigate(['/search'], { queryParams: { q } });
    this.execute(q);
  }

  private execute(q: string): void {
    this.submitted.set(true);
    this.loading.set(true);
    this.api.search(q).subscribe({
      next: (results) => {
        this.results.set(results);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
