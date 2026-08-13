import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AccountOverviewDto,
  BuyerAddressDto,
  BuyerConversationDto,
  BuyerOrderDto,
  BuyerOrdersPage,
  BuyerProfileDto,
  ChatMessageDto,
  CreateReviewRequest,
  FavoritesDto,
  NotificationsPage,
  ReviewDto,
  SaveAddressRequest,
  UpdateBuyerProfileRequest,
} from '../models/api-types';

/** The signed-in buyer's private account area. */
@Injectable({ providedIn: 'root' })
export class AccountApiService {
  private readonly http = inject(HttpClient);

  overview(): Observable<AccountOverviewDto> {
    return this.http.get<AccountOverviewDto>('/api/account');
  }

  // ── Profile ──────────────────────────────────────────────────────────
  profile(): Observable<BuyerProfileDto> {
    return this.http.get<BuyerProfileDto>('/api/account/profile');
  }

  updateProfile(request: UpdateBuyerProfileRequest): Observable<BuyerProfileDto> {
    return this.http.put<BuyerProfileDto>('/api/account/profile', request);
  }

  uploadAvatar(file: File): Observable<BuyerProfileDto> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<BuyerProfileDto>('/api/account/profile/avatar', form);
  }

  // ── Orders ───────────────────────────────────────────────────────────
  orders(filter: string, page = 1, pageSize = 10): Observable<BuyerOrdersPage> {
    const params = new HttpParams().set('filter', filter).set('page', page).set('pageSize', pageSize);
    return this.http.get<BuyerOrdersPage>('/api/account/orders', { params });
  }

  order(id: string): Observable<BuyerOrderDto> {
    return this.http.get<BuyerOrderDto>(`/api/account/orders/${id}`);
  }

  review(orderId: string, request: CreateReviewRequest): Observable<ReviewDto> {
    return this.http.post<ReviewDto>(`/api/account/orders/${orderId}/review`, request);
  }

  // ── Messages ─────────────────────────────────────────────────────────
  conversations(search?: string): Observable<BuyerConversationDto[]> {
    const params = search ? new HttpParams().set('search', search) : new HttpParams();
    return this.http.get<BuyerConversationDto[]>('/api/account/conversations', { params });
  }

  messages(orderId: string): Observable<ChatMessageDto[]> {
    return this.http.get<ChatMessageDto[]>(`/api/account/orders/${orderId}/messages`);
  }

  sendMessage(orderId: string, body: string): Observable<ChatMessageDto> {
    return this.http.post<ChatMessageDto>(`/api/account/orders/${orderId}/messages`, { body });
  }

  // ── Favorites ────────────────────────────────────────────────────────
  favorites(): Observable<FavoritesDto> {
    return this.http.get<FavoritesDto>('/api/account/favorites');
  }

  // ── Addresses ────────────────────────────────────────────────────────
  addresses(): Observable<BuyerAddressDto[]> {
    return this.http.get<BuyerAddressDto[]>('/api/account/addresses');
  }

  createAddress(request: SaveAddressRequest): Observable<BuyerAddressDto> {
    return this.http.post<BuyerAddressDto>('/api/account/addresses', request);
  }

  updateAddress(id: string, request: SaveAddressRequest): Observable<BuyerAddressDto> {
    return this.http.put<BuyerAddressDto>(`/api/account/addresses/${id}`, request);
  }

  deleteAddress(id: string): Observable<unknown> {
    return this.http.delete(`/api/account/addresses/${id}`);
  }

  setDefaultAddress(id: string): Observable<BuyerAddressDto> {
    return this.http.post<BuyerAddressDto>(`/api/account/addresses/${id}/default`, null);
  }

  // ── Notifications (same inbox endpoint the dashboard uses) ───────────
  notifications(limit = 30): Observable<NotificationsPage> {
    return this.http.get<NotificationsPage>('/api/notifications', { params: { limit } });
  }

  markNotificationsRead(): Observable<unknown> {
    return this.http.post('/api/notifications/read-all', null);
  }
}
