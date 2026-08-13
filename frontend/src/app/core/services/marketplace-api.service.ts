import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  MarketplaceHomeDto,
  ProductCardDto,
  PublicOfferDto,
  SearchResultsDto,
  SearchSuggestionsDto,
  StoreReviewsPage,
  StoresPage,
  TrackActivityRequest,
} from '../models/api-types';

export interface StoreDirectoryFilters {
  search?: string;
  governorate?: string;
  delivery?: boolean;
  pickup?: boolean;
  openNow?: boolean;
  hasOffers?: boolean;
  sort?: string;
  page?: number;
  pageSize?: number;
}

/** Cross-store marketplace reads. Anonymous; personalized automatically when signed in. */
@Injectable({ providedIn: 'root' })
export class MarketplaceApiService {
  private readonly http = inject(HttpClient);

  home(): Observable<MarketplaceHomeDto> {
    return this.http.get<MarketplaceHomeDto>('/api/public/home');
  }

  stores(filters: StoreDirectoryFilters): Observable<StoresPage> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<StoresPage>('/api/public/stores', { params });
  }

  search(q: string): Observable<SearchResultsDto> {
    return this.http.get<SearchResultsDto>('/api/public/search', { params: { q } });
  }

  suggest(q: string): Observable<SearchSuggestionsDto> {
    const params = q ? new HttpParams().set('q', q) : new HttpParams();
    return this.http.get<SearchSuggestionsDto>('/api/public/search/suggest', { params });
  }

  offers(count = 12): Observable<PublicOfferDto[]> {
    return this.http.get<PublicOfferDto[]>('/api/public/offers', { params: { count } });
  }

  relatedProducts(productId: string, count = 8): Observable<ProductCardDto[]> {
    return this.http.get<ProductCardDto[]>(`/api/public/products/${productId}/related`, {
      params: { count },
    });
  }

  storeReviews(slug: string, page = 1, pageSize = 10): Observable<StoreReviewsPage> {
    return this.http.get<StoreReviewsPage>(`/api/public/stores/${slug}/reviews`, {
      params: { page, pageSize },
    });
  }

  /** Fire-and-forget shopping signal; errors are deliberately swallowed. */
  track(event: TrackActivityRequest): void {
    this.http.post('/api/public/activity', event).subscribe({ error: () => undefined });
  }
}
