import { Component, inject, signal } from '@angular/core';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MarketplaceApiService } from '../../core/services/marketplace-api.service';
import { StoreCardDto } from '../../core/models/api-types';
import { EmptyState } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';
import { CardSkeletons, StoreCard } from './cards';

const GOVERNORATES = [
  'Muscat', 'Dhofar', 'Musandam', 'Al Buraimi', 'Ad Dakhiliyah',
  'Al Batinah North', 'Al Batinah South', 'Ash Sharqiyah North',
  'Ash Sharqiyah South', 'Ad Dhahirah', 'Al Wusta',
];

/** Store directory with filters, sorting and load-more paging. */
@Component({
  selector: 'wo-stores-page',
  imports: [TranslatePipe, StoreCard, CardSkeletons, EmptyState],
  template: `
    <div class="container page">
      <h1>{{ 'market.storesTitle' | t }}</h1>

      <div class="filters">
        <input
          class="search"
          type="search"
          [placeholder]="'market.filterSearch' | t"
          (input)="search$.next($any($event.target).value)"
        />
        <select (change)="setGovernorate($any($event.target).value)">
          <option value="">{{ 'market.allGovernorates' | t }}</option>
          @for (gov of governorates; track gov) {
            <option [value]="gov" [selected]="governorate() === gov">{{ gov }}</option>
          }
        </select>
        <select (change)="setSort($any($event.target).value)">
          <option value="recommended">{{ 'market.sort.recommended' | t }}</option>
          <option value="popular">{{ 'market.sort.popular' | t }}</option>
          <option value="rating">{{ 'market.sort.rating' | t }}</option>
          <option value="newest">{{ 'market.sort.newest' | t }}</option>
          <option value="deliveryfee">{{ 'market.sort.deliveryFee' | t }}</option>
        </select>
      </div>

      <div class="chips">
        <button class="chip" [class.on]="openNow()" (click)="toggle(openNow)">
          {{ 'market.filterOpenNow' | t }}
        </button>
        <button class="chip" [class.on]="delivery()" (click)="toggle(delivery)">
          🛵 {{ 'market.filterDelivery' | t }}
        </button>
        <button class="chip" [class.on]="pickup()" (click)="toggle(pickup)">
          🛍️ {{ 'market.filterPickup' | t }}
        </button>
        <button class="chip" [class.on]="hasOffers()" (click)="toggle(hasOffers)">
          🏷️ {{ 'market.filterOffers' | t }}
        </button>
      </div>

      @if (loading() && stores().length === 0) {
        <div class="grid"><wo-card-skeletons [count]="8" /></div>
      } @else if (stores().length === 0) {
        <wo-empty-state emoji="🔍" messageKey="market.noStores" />
      } @else {
        <div class="grid">
          @for (store of stores(); track store.id) {
            <wo-store-card [store]="store" />
          }
        </div>
        @if (stores().length < total()) {
          <div class="more">
            <button class="wo-btn wo-btn--ghost" (click)="loadMore()" [disabled]="loading()">
              {{ (loading() ? 'common.loading' : 'market.loadMore') | t }}
            </button>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .page { padding-block: 24px 46px; }
    h1 { margin: 0 0 16px; font-size: 24px; font-weight: 800; }
    .filters { display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; }
    .search { flex: 1 1 220px; }
    .search, select {
      border: 1px solid var(--wo-border); border-radius: var(--wo-radius-sm);
      background: var(--wo-surface); padding: 9px 12px; font: inherit; outline: none;
    }
    .search:focus, select:focus { border-color: var(--wo-primary); }
    .chips { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 18px; }
    .chip {
      border: 1px solid var(--wo-border); background: var(--wo-surface); cursor: pointer;
      padding: 7px 14px; border-radius: 99px; font: inherit; font-size: 13.5px; font-weight: 600;
      color: var(--wo-ink-soft);
    }
    .chip.on { background: var(--wo-primary-soft); border-color: var(--wo-primary); color: var(--wo-primary-dark); }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 14px; }
    .more { text-align: center; margin-top: 22px; }
    @media (max-width: 700px) { .grid { grid-template-columns: 1fr 1fr; } }
    @media (max-width: 460px) { .grid { grid-template-columns: 1fr; } }
  `,
})
export class StoresPage {
  private readonly api = inject(MarketplaceApiService);

  protected readonly governorates = GOVERNORATES;
  readonly search$ = new Subject<string>();

  readonly loading = signal(true);
  readonly stores = signal<StoreCardDto[]>([]);
  readonly total = signal(0);
  readonly search = signal('');
  readonly governorate = signal('');
  readonly sort = signal('recommended');
  readonly openNow = signal(false);
  readonly delivery = signal(false);
  readonly pickup = signal(false);
  readonly hasOffers = signal(false);
  private page = 1;

  constructor() {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((term) => {
        this.search.set(term);
        this.reload();
      });
    this.reload();
  }

  toggle(flag: ReturnType<typeof signal<boolean>>): void {
    flag.update((v) => !v);
    this.reload();
  }

  setGovernorate(value: string): void {
    this.governorate.set(value);
    this.reload();
  }

  setSort(value: string): void {
    this.sort.set(value);
    this.reload();
  }

  loadMore(): void {
    this.page += 1;
    this.fetch(false);
  }

  private reload(): void {
    this.page = 1;
    this.fetch(true);
  }

  private fetch(replace: boolean): void {
    this.loading.set(true);
    this.api
      .stores({
        search: this.search() || undefined,
        governorate: this.governorate() || undefined,
        openNow: this.openNow() || undefined,
        delivery: this.delivery() || undefined,
        pickup: this.pickup() || undefined,
        hasOffers: this.hasOffers() || undefined,
        sort: this.sort(),
        page: this.page,
        pageSize: 12,
      })
      .subscribe({
        next: (result) => {
          this.stores.update((current) => (replace ? result.items : [...current, ...result.items]));
          this.total.set(result.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
