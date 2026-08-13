import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, shareReplay, tap } from 'rxjs';
import { AuthResponse, UserDto } from '../models/api-types';

interface StoredAuth {
  accessToken: string;
  refreshToken: string;
  user: UserDto;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly STORAGE_KEY = 'wo.auth';

  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly state = signal<StoredAuth | null>(this.restore());

  readonly user = computed(() => this.state()?.user ?? null);
  readonly isAuthenticated = computed(() => this.state() !== null);
  /** Sessions stored before roles existed belong to sellers — default accordingly. */
  readonly isSeller = computed(() => {
    const user = this.user();
    if (!user) return false;
    return user.roles ? user.roles.includes('Owner') : true;
  });
  readonly accessToken = () => this.state()?.accessToken ?? null;

  private refreshInFlight?: Observable<AuthResponse>;

  register(
    email: string,
    password: string,
    displayName: string,
    accountType: 'seller' | 'buyer' = 'seller',
  ): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/register', { email, password, displayName, accountType })
      .pipe(tap((auth) => this.persist(auth)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', { email, password })
      .pipe(tap((auth) => this.persist(auth)));
  }

  /** Single-flight refresh shared by concurrent 401 retries. */
  refresh(): Observable<AuthResponse> {
    if (!this.refreshInFlight) {
      const refreshToken = this.state()?.refreshToken ?? '';
      this.refreshInFlight = this.http
        .post<AuthResponse>('/api/auth/refresh', { refreshToken })
        .pipe(
          tap((auth) => this.persist(auth)),
          finalize(() => (this.refreshInFlight = undefined)),
          shareReplay(1),
        );
    }
    return this.refreshInFlight;
  }

  forgotPassword(email: string): Observable<unknown> {
    return this.http.post('/api/auth/forgot-password', { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<unknown> {
    return this.http.post('/api/auth/reset-password', { email, token, newPassword });
  }

  /** Re-syncs the user snapshot (e.g. hasStore after onboarding). */
  refreshMe(): Observable<UserDto> {
    return this.http.get<UserDto>('/api/auth/me').pipe(tap((user) => this.patchUser(user)));
  }

  patchUser(user: UserDto): void {
    const current = this.state();
    if (current) this.state.set({ ...current, user });
    this.save();
  }

  logout(): void {
    const refreshToken = this.state()?.refreshToken;
    if (refreshToken) {
      this.http.post('/api/auth/logout', { refreshToken }).subscribe({ error: () => undefined });
    }
    this.clear();
    void this.router.navigate(['/']);
  }

  /** Used by the interceptor when a refresh attempt itself fails. */
  clear(): void {
    this.state.set(null);
    localStorage.removeItem(AuthService.STORAGE_KEY);
  }

  private persist(auth: AuthResponse): void {
    this.state.set({
      accessToken: auth.accessToken,
      refreshToken: auth.refreshToken,
      user: auth.user,
    });
    this.save();
  }

  private save(): void {
    const value = this.state();
    if (value) localStorage.setItem(AuthService.STORAGE_KEY, JSON.stringify(value));
  }

  private restore(): StoredAuth | null {
    try {
      const raw = localStorage.getItem(AuthService.STORAGE_KEY);
      return raw ? (JSON.parse(raw) as StoredAuth) : null;
    } catch {
      return null;
    }
  }
}
