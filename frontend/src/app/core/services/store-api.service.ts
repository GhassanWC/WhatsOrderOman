import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CategoryDto, ChatMessageDto, CreateStoreRequest, DashboardSummaryDto, NotificationsPage,
  OfferDto, OrderDto, OrderStatus, OrdersPage, PagedResult, ProductDto, SaveCategoryRequest,
  SaveOfferRequest, SaveProductRequest, SlugAvailabilityDto, StoreDto, StoreReviewsPage,
  SubscriptionDto, UpdateStoreRequest, UpdateStoreSettingsRequest,
} from '../models/api-types';

/** Owner-side API. The backend resolves the tenant from the JWT — never from parameters. */
@Injectable({ providedIn: 'root' })
export class StoreApiService {
  private readonly http = inject(HttpClient);

  // Store
  getMyStore(): Observable<StoreDto> {
    return this.http.get<StoreDto>('/api/store');
  }

  createStore(request: CreateStoreRequest): Observable<StoreDto> {
    return this.http.post<StoreDto>('/api/store', request);
  }

  updateStore(request: UpdateStoreRequest): Observable<StoreDto> {
    return this.http.put<StoreDto>('/api/store', request);
  }

  updateSettings(request: UpdateStoreSettingsRequest): Observable<StoreDto> {
    return this.http.put<StoreDto>('/api/store/settings', request);
  }

  uploadLogo(file: File): Observable<StoreDto> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<StoreDto>('/api/store/logo', form);
  }

  removeLogo(): Observable<StoreDto> {
    return this.http.delete<StoreDto>('/api/store/logo');
  }

  uploadBanner(file: File): Observable<StoreDto> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<StoreDto>('/api/store/banner', form);
  }

  removeBanner(): Observable<StoreDto> {
    return this.http.delete<StoreDto>('/api/store/banner');
  }

  getStoreReviews(page = 1, pageSize = 10): Observable<StoreReviewsPage> {
    return this.http.get<StoreReviewsPage>('/api/store/reviews', { params: { page, pageSize } });
  }

  checkSlug(slug: string): Observable<SlugAvailabilityDto> {
    return this.http.get<SlugAvailabilityDto>('/api/store/slug-available', {
      params: { slug },
    });
  }

  // Categories
  getCategories(): Observable<CategoryDto[]> {
    return this.http.get<CategoryDto[]>('/api/categories');
  }

  createCategory(request: SaveCategoryRequest): Observable<CategoryDto> {
    return this.http.post<CategoryDto>('/api/categories', request);
  }

  updateCategory(id: string, request: SaveCategoryRequest): Observable<CategoryDto> {
    return this.http.put<CategoryDto>(`/api/categories/${id}`, request);
  }

  deleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`/api/categories/${id}`);
  }

  // Products
  getProducts(search?: string, categoryId?: string, page = 1, pageSize = 50): Observable<PagedResult<ProductDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (categoryId) params = params.set('categoryId', categoryId);
    return this.http.get<PagedResult<ProductDto>>('/api/products', { params });
  }

  getProduct(id: string): Observable<ProductDto> {
    return this.http.get<ProductDto>(`/api/products/${id}`);
  }

  createProduct(request: SaveProductRequest): Observable<ProductDto> {
    return this.http.post<ProductDto>('/api/products', request);
  }

  updateProduct(id: string, request: SaveProductRequest): Observable<ProductDto> {
    return this.http.put<ProductDto>(`/api/products/${id}`, request);
  }

  deleteProduct(id: string): Observable<void> {
    return this.http.delete<void>(`/api/products/${id}`);
  }

  setProductAvailability(id: string, isAvailable: boolean): Observable<ProductDto> {
    return this.http.patch<ProductDto>(`/api/products/${id}/availability`, { isAvailable });
  }

  addProductImage(id: string, file: File): Observable<ProductDto> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ProductDto>(`/api/products/${id}/images`, form);
  }

  deleteProductImage(id: string, imageId: string): Observable<ProductDto> {
    return this.http.delete<ProductDto>(`/api/products/${id}/images/${imageId}`);
  }

  reorderProductImages(id: string, imageIds: string[]): Observable<ProductDto> {
    return this.http.put<ProductDto>(`/api/products/${id}/images/order`, { imageIds });
  }

  setPrimaryProductImage(id: string, imageId: string): Observable<ProductDto> {
    return this.http.patch<ProductDto>(`/api/products/${id}/images/${imageId}/primary`, null);
  }

  // Offers
  getOffers(): Observable<OfferDto[]> {
    return this.http.get<OfferDto[]>('/api/offers');
  }

  createOffer(request: SaveOfferRequest): Observable<OfferDto> {
    return this.http.post<OfferDto>('/api/offers', request);
  }

  updateOffer(id: string, request: SaveOfferRequest): Observable<OfferDto> {
    return this.http.put<OfferDto>(`/api/offers/${id}`, request);
  }

  deleteOffer(id: string): Observable<void> {
    return this.http.delete<void>(`/api/offers/${id}`);
  }

  // Orders
  getOrders(filter?: string, search?: string, page = 1, pageSize = 30): Observable<OrdersPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filter) params = params.set('filter', filter);
    if (search) params = params.set('search', search);
    return this.http.get<OrdersPage>('/api/orders', { params });
  }

  getOrder(id: string): Observable<OrderDto> {
    return this.http.get<OrderDto>(`/api/orders/${id}`);
  }

  updateOrderStatus(id: string, status: OrderStatus, estimatedMinutes?: number | null): Observable<OrderDto> {
    return this.http.patch<OrderDto>(`/api/orders/${id}/status`, {
      status,
      estimatedMinutes: estimatedMinutes ?? null,
    });
  }

  // Order chat (owner side)
  getOrderMessages(orderId: string): Observable<ChatMessageDto[]> {
    return this.http.get<ChatMessageDto[]>(`/api/orders/${orderId}/messages`);
  }

  sendOrderMessage(orderId: string, body: string): Observable<ChatMessageDto> {
    return this.http.post<ChatMessageDto>(`/api/orders/${orderId}/messages`, { body });
  }

  // Notifications
  getNotifications(limit = 30): Observable<NotificationsPage> {
    return this.http.get<NotificationsPage>('/api/notifications', { params: { limit } });
  }

  markNotificationsRead(): Observable<void> {
    return this.http.post<void>('/api/notifications/read-all', null);
  }

  // Dashboard & subscription
  getDashboardSummary(): Observable<DashboardSummaryDto> {
    return this.http.get<DashboardSummaryDto>('/api/dashboard/summary');
  }

  getSubscription(): Observable<SubscriptionDto> {
    return this.http.get<SubscriptionDto>('/api/subscription');
  }

  changePlan(plan: string): Observable<SubscriptionDto> {
    return this.http.post<SubscriptionDto>('/api/subscription/plan', { plan });
  }
}
