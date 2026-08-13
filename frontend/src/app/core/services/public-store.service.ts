import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChatMessageDto, CreatePublicOrderRequest, PublicOrderCreatedDto, PublicOrderStatusDto,
  PublicProductDto, PublicStoreDto,
} from '../models/api-types';

/** Customer-facing endpoints — no authentication, ever. */
@Injectable({ providedIn: 'root' })
export class PublicStoreService {
  private readonly http = inject(HttpClient);

  getStore(slug: string): Observable<PublicStoreDto> {
    return this.http.get<PublicStoreDto>(`/api/public/stores/${slug}`);
  }

  getProducts(slug: string, search?: string, categoryId?: string): Observable<PublicProductDto[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (categoryId) params = params.set('categoryId', categoryId);
    return this.http.get<PublicProductDto[]>(`/api/public/stores/${slug}/products`, { params });
  }

  getProduct(slug: string, productId: string): Observable<PublicProductDto> {
    return this.http.get<PublicProductDto>(`/api/public/stores/${slug}/products/${productId}`);
  }

  createOrder(slug: string, request: CreatePublicOrderRequest): Observable<PublicOrderCreatedDto> {
    return this.http.post<PublicOrderCreatedDto>(`/api/public/stores/${slug}/orders`, request);
  }

  getOrderStatus(slug: string, orderNumber: string, phone: string): Observable<PublicOrderStatusDto> {
    return this.http.get<PublicOrderStatusDto>(
      `/api/public/stores/${slug}/orders/${orderNumber}`,
      { params: { phone } },
    );
  }

  // Order chat (customer side — same slug+orderNumber+phone proof as the status lookup)
  getOrderMessages(slug: string, orderNumber: string, phone: string): Observable<ChatMessageDto[]> {
    return this.http.get<ChatMessageDto[]>(
      `/api/public/stores/${slug}/orders/${orderNumber}/messages`,
      { params: { phone } },
    );
  }

  sendOrderMessage(slug: string, orderNumber: string, phone: string, body: string): Observable<ChatMessageDto> {
    return this.http.post<ChatMessageDto>(
      `/api/public/stores/${slug}/orders/${orderNumber}/messages`,
      { body },
      { params: { phone } },
    );
  }
}
