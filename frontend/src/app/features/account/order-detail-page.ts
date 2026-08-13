import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountApiService } from '../../core/services/account-api.service';
import { BuyerRealtimeService } from '../../core/realtime/realtime.service';
import { BuyerOrderDto, ChatMessageDto } from '../../core/models/api-types';
import { ChatThread } from '../../shared/chat-thread';
import { StatusChip, problemMessage } from '../../shared/components';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';
import { buildTimeline } from '../storefront/order-confirmation-page';

/** One order in the buyer's account: live timeline, items, chat and review. */
@Component({
  selector: 'wo-account-order-detail',
  imports: [RouterLink, DatePipe, FormsModule, TranslatePipe, BilingualPipe, OmrPricePipe, StatusChip, ChatThread],
  template: `
    @if (order(); as o) {
      <nav class="crumbs"><a routerLink="/account/orders">← {{ 'account.nav.orders' | t }}</a></nav>

      <header class="head wo-card">
        <div class="logo">
          @if (o.storeLogoUrl; as logo) {
            <img [src]="logo" alt="" />
          } @else {
            <span>{{ o.storeName.charAt(0) }}</span>
          }
        </div>
        <div class="mid">
          <h1>{{ o.storeName | bilingual: o.storeNameAr }}</h1>
          <small class="muted">#{{ o.orderNumber }} · {{ o.createdAt | date: 'medium' }}</small>
        </div>
        <div class="head-end">
          <wo-status-chip [status]="o.status" />
          <a [routerLink]="['/', o.storeSlug]" class="wo-btn wo-btn--ghost mini">{{ 'account.viewStore' | t }}</a>
        </div>
      </header>

      <div class="grid">
        <div class="col">
          <!-- ── Timeline ────────────────────────────────────────────── -->
          <section class="wo-card panel">
            <h2>{{ 'account.orderProgress' | t }}</h2>
            <ol class="timeline">
              @for (step of timeline(); track step.status) {
                <li [class.done]="step.done" [class.current]="step.current"
                    [class.terminal]="step.status === 'Cancelled' || step.status === 'Rejected'">
                  <span class="dot"></span>
                  <div>
                    <b>{{ 'orders.status.' + step.status | t }}</b>
                    @if (step.at) { <small>{{ step.at | date: 'shortTime' }}</small> }
                  </div>
                </li>
              }
            </ol>
            @if (o.estimatedReadyAt) {
              <p class="eta">⏱ {{ 'account.estimatedReady' | t }}: {{ o.estimatedReadyAt | date: 'shortTime' }}</p>
            }
          </section>

          <!-- ── Items + totals ──────────────────────────────────────── -->
          <section class="wo-card panel">
            <h2>{{ 'account.orderItems' | t }}</h2>
            <ul class="items">
              @for (item of o.items; track item.id) {
                <li>
                  <span class="qty">{{ item.quantity }}×</span>
                  <span class="iname">{{ item.productName }}
                    @if (item.variantsText) { <small class="muted">{{ item.variantsText }}</small> }
                  </span>
                  <span>{{ item.lineTotal | omr }}</span>
                </li>
              }
            </ul>
            <div class="totals">
              <div><span>{{ 'cart.subtotal' | t }}</span><span>{{ o.subtotal | omr }}</span></div>
              @if (o.deliveryFee > 0) {
                <div><span>{{ 'cart.deliveryFee' | t }}</span><span>{{ o.deliveryFee | omr }}</span></div>
              }
              @if (o.discount > 0) {
                <div class="save"><span>{{ 'cart.savings' | t }}</span><span>−{{ o.discount | omr }}</span></div>
              }
              <div class="grand"><span>{{ 'cart.total' | t }}</span><span>{{ o.total | omr }}</span></div>
            </div>
            @if (o.deliveryAddress) {
              <p class="muted small">📍 {{ o.deliveryAddress }}</p>
            }
          </section>

          <!-- ── Review ──────────────────────────────────────────────── -->
          @if (o.review; as review) {
            <section class="wo-card panel">
              <h2>{{ 'account.yourReview' | t }}</h2>
              <p class="stars">{{ '★★★★★'.slice(0, review.rating) }}<span class="dim">{{ '★★★★★'.slice(review.rating) }}</span></p>
              @if (review.comment) { <p class="muted">{{ review.comment }}</p> }
            </section>
          } @else if (o.canReview) {
            <section class="wo-card panel" id="review">
              <h2>{{ 'account.leaveReview' | t }}</h2>
              <div class="star-picker">
                @for (star of [1, 2, 3, 4, 5]; track star) {
                  <button type="button" [class.lit]="star <= reviewRating()" (click)="reviewRating.set(star)">★</button>
                }
              </div>
              <textarea rows="3" [ngModel]="reviewComment()" (ngModelChange)="reviewComment.set($event)"
                        [placeholder]="'account.reviewPlaceholder' | t"></textarea>
              @if (reviewError()) { <p class="error-banner">{{ reviewError() }}</p> }
              <button class="wo-btn wo-btn--primary" (click)="submitReview()"
                      [disabled]="reviewRating() === 0 || reviewBusy()">
                {{ 'account.submitReview' | t }}
              </button>
            </section>
          }
        </div>

        <!-- ── Chat ────────────────────────────────────────────────────── -->
        <section class="wo-card panel chat-panel" id="chat">
          <h2>💬 {{ 'account.chatWithStore' | t }}</h2>
          <wo-chat-thread
            [messages]="messages()"
            self="Customer"
            [typing]="storeTyping()"
            [sending]="sending()"
            [connected]="realtime.connected()"
            (send)="sendMessage($event)"
            (typingChange)="realtime.sendTyping(orderId(), $event)"
          />
        </section>
      </div>
    } @else {
      <div class="wo-skeleton" style="height: 90px; margin-bottom: 14px"></div>
      <div class="wo-skeleton" style="height: 300px"></div>
    }
  `,
  styles: `
    .crumbs { margin-bottom: 12px;
      a { text-decoration: none; color: var(--wo-ink-soft); font-weight: 600; font-size: 14px; } }
    .head { display: flex; align-items: center; gap: 14px; padding: 16px 18px; margin-bottom: 14px; flex-wrap: wrap; }
    .logo { width: 52px; height: 52px; border-radius: 14px; overflow: hidden; flex-shrink: 0;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      display: grid; place-items: center; font-weight: 800; font-size: 20px; }
    .logo img { width: 100%; height: 100%; object-fit: cover; }
    .mid { flex: 1; min-width: 0;
      h1 { margin: 0; font-size: 19px; } }
    .head-end { display: flex; align-items: center; gap: 10px; }
    .mini { padding: 6px 12px; font-size: 12.5px; }
    .grid { display: grid; grid-template-columns: 1.2fr 1fr; gap: 14px; align-items: start;
      @media (max-width: 900px) { grid-template-columns: 1fr; } }
    .col { display: grid; gap: 14px; min-width: 0; }
    .panel { padding: 16px 18px;
      h2 { margin: 0 0 12px; font-size: 16px; } }
    .timeline { list-style: none; margin: 0; padding: 0; display: grid; gap: 0; }
    .timeline li { display: flex; gap: 12px; align-items: flex-start; padding-bottom: 16px; position: relative;
      b { font-size: 14px; color: var(--wo-muted); }
      small { display: block; color: var(--wo-muted); font-size: 12px; } }
    .timeline li::before { content: ''; position: absolute; inset-inline-start: 7px; top: 18px; bottom: 0;
      width: 2px; background: var(--wo-border); }
    .timeline li:last-child::before { display: none; }
    .dot { width: 16px; height: 16px; border-radius: 50%; border: 2px solid var(--wo-border);
      background: var(--wo-surface); flex-shrink: 0; margin-top: 2px; position: relative; z-index: 1; }
    .timeline li.done b { color: var(--wo-ink); }
    .timeline li.done .dot { border-color: var(--wo-primary); background: var(--wo-primary); }
    .timeline li.current b { color: var(--wo-primary-dark); }
    .timeline li.terminal .dot { border-color: var(--wo-danger); background: var(--wo-danger); }
    .timeline li.terminal b { color: var(--wo-danger); }
    .eta { margin: 4px 0 0; font-size: 13.5px; color: var(--wo-ink-soft); }
    .items { list-style: none; margin: 0 0 12px; padding: 0; display: grid; gap: 8px;
      li { display: flex; gap: 8px; font-size: 14px; align-items: baseline; }
      .qty { font-weight: 700; color: var(--wo-primary-dark); }
      .iname { flex: 1; small { display: block; font-size: 12px; } } }
    .totals { border-top: 1px dashed var(--wo-border); padding-top: 10px; display: grid; gap: 5px;
      div { display: flex; justify-content: space-between; font-size: 14px; color: var(--wo-ink-soft); }
      .save { color: #15803d; }
      .grand { font-weight: 800; color: var(--wo-ink); font-size: 16px; } }
    .small { font-size: 13px; margin: 10px 0 0; }
    .stars { font-size: 20px; color: #f59e0b; letter-spacing: 3px; margin: 0 0 6px;
      .dim { color: var(--wo-border); } }
    .star-picker { display: flex; gap: 4px; margin-bottom: 10px;
      button { border: none; background: none; font-size: 30px; color: var(--wo-border); cursor: pointer; padding: 0 2px;
        &.lit { color: #f59e0b; } } }
    textarea { width: 100%; border: 1.5px solid var(--wo-border); border-radius: var(--wo-radius-sm);
      padding: 10px 12px; font: inherit; outline: none; resize: vertical; margin-bottom: 10px;
      &:focus { border-color: var(--wo-primary); } }
    .error-banner { background: #fef2f2; color: var(--wo-danger); border-radius: 10px; padding: 10px 14px; font-size: 14px; margin-bottom: 10px; }
    .chat-panel { position: sticky; top: 76px; }
    @media (max-width: 900px) { .chat-panel { position: static; } }
  `,
})
export class AccountOrderDetailPage {
  readonly orderId = input.required<string>();

  private readonly api = inject(AccountApiService);
  protected readonly realtime = inject(BuyerRealtimeService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly order = signal<BuyerOrderDto | null>(null);
  readonly messages = signal<ChatMessageDto[]>([]);
  readonly sending = signal(false);
  readonly storeTyping = signal(false);
  readonly reviewRating = signal(0);
  readonly reviewComment = signal('');
  readonly reviewBusy = signal(false);
  readonly reviewError = signal<string | null>(null);

  readonly timeline = computed(() => {
    const o = this.order();
    return o ? buildTimeline(o) : [];
  });

  private typingTimer: ReturnType<typeof setTimeout> | null = null;
  private watchedOrderId: string | null = null;

  constructor() {
    this.realtime.start();

    effect(() => {
      const id = this.orderId();
      if (id && id !== this.watchedOrderId) {
        if (this.watchedOrderId) this.realtime.unwatchOrder(this.watchedOrderId);
        this.watchedOrderId = id;
        this.realtime.watchOrder(id);
        this.load(id);
      }
    });

    this.realtime.statusChanged$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      if (evt.orderId === this.orderId()) this.load(this.orderId());
    });
    this.realtime.messageReceived$.pipe(takeUntilDestroyed()).subscribe((message) => {
      if (message.orderId !== this.orderId()) return;
      this.messages.update((list) =>
        list.some((m) => m.id === message.id) ? list : [...list, message]);
      this.storeTyping.set(false);
    });
    this.realtime.messagesRead$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      if (evt.orderId !== this.orderId() || evt.reader !== 'Store') return;
      this.messages.update((list) =>
        list.map((m) => (m.sender === 'Customer' && !m.readAt ? { ...m, readAt: evt.readAt } : m)));
    });
    this.realtime.typing$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      if (evt.orderId !== this.orderId() || evt.sender !== 'Store') return;
      this.storeTyping.set(evt.isTyping);
      if (this.typingTimer) clearTimeout(this.typingTimer);
      if (evt.isTyping) this.typingTimer = setTimeout(() => this.storeTyping.set(false), 4000);
    });
    this.realtime.reconnected$.pipe(takeUntilDestroyed()).subscribe(() => {
      const id = this.orderId();
      this.realtime.watchOrder(id);
      this.load(id);
    });

    this.destroyRef.onDestroy(() => {
      if (this.watchedOrderId) this.realtime.unwatchOrder(this.watchedOrderId);
    });
  }

  private load(id: string): void {
    this.api.order(id).subscribe({
      next: (order) => this.order.set(order),
      error: () => undefined,
    });
    this.api.messages(id).subscribe({
      next: (messages) => this.messages.set(messages),
      error: () => undefined,
    });
  }

  sendMessage(body: string): void {
    if (this.sending()) return;
    this.sending.set(true);
    this.api.sendMessage(this.orderId(), body).subscribe({
      next: (message) => {
        this.messages.update((list) =>
          list.some((m) => m.id === message.id) ? list : [...list, message]);
        this.sending.set(false);
      },
      error: () => this.sending.set(false),
    });
  }

  submitReview(): void {
    if (this.reviewRating() === 0 || this.reviewBusy()) return;
    this.reviewBusy.set(true);
    this.reviewError.set(null);
    this.api.review(this.orderId(), {
      rating: this.reviewRating(),
      comment: this.reviewComment().trim() || null,
    }).subscribe({
      next: () => {
        this.reviewBusy.set(false);
        this.snackBar.open(this.i18n.t('account.reviewThanks'), undefined, { duration: 2500 });
        this.load(this.orderId());
      },
      error: (err) => {
        this.reviewBusy.set(false);
        this.reviewError.set(problemMessage(err, this.i18n.t('common.error')));
      },
    });
  }
}
