import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { StoreApiService } from '../../core/services/store-api.service';
import {
  ChatMessageDto, FulfillmentMethod, OrderDto, OrderListItemDto, OrderStatus,
} from '../../core/models/api-types';
import { OwnerRealtimeService } from '../../core/realtime/realtime.service';
import { ConfirmDialog, EmptyState, StatusChip, problemMessage } from '../../shared/components';
import { ChatThread } from '../../shared/chat-thread';
import { OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { whatsAppLink } from '../../shared/whatsapp';
import { TranslationService } from '../../core/i18n/translation.service';

const FILTERS = ['all', 'today', 'new', 'preparing', 'completed', 'cancelled', 'rejected'] as const;
type OrderFilter = (typeof FILTERS)[number];

const ESTIMATE_CHOICES = [15, 30, 45, 60, 90];

@Component({
  selector: 'wo-orders-page',
  imports: [DatePipe, FormsModule, StatusChip, EmptyState, ChatThread, OmrPricePipe, TranslatePipe],
  template: `
    <div class="head">
      <h1>{{ 'orders.title' | t }}</h1>
      @if (newCount() > 0) {
        <span class="new-badge">{{ newCount() }} {{ 'orders.filter.new' | t }}</span>
      }
      <span class="live" [class.live--on]="realtime.connected()">
        ● {{ (realtime.connected() ? 'orders.live' : 'orders.offline') | t }}
      </span>
    </div>

    <div class="toolbar">
      <div class="chips">
        @for (item of filters; track item) {
          <button type="button" class="chip" [class.chip--on]="filter() === item" (click)="setFilter(item)">
            {{ 'orders.filter.' + item | t }}
          </button>
        }
      </div>
      <input class="search" type="search" [placeholder]="'orders.searchPlaceholder' | t"
             [ngModel]="search()" (ngModelChange)="onSearch($event)" />
    </div>

    @if (loading()) {
      <div class="list">
        @for (i of [1, 2, 3]; track i) {
          <div class="wo-skeleton" style="height: 110px"></div>
        }
      </div>
    } @else if (orders().length === 0) {
      <wo-empty-state emoji="🧾" messageKey="orders.empty" />
    } @else {
      <div class="list">
        @for (order of orders(); track order.id) {
          <div class="order wo-card" [class.order--open]="expandedId() === order.id">
            <button type="button" class="order__summary" (click)="toggle(order)">
              <div class="col main">
                <div class="line1">
                  <span class="num">#{{ order.orderNumber }}</span>
                  <wo-status-chip [status]="order.status" />
                  <span class="method">{{ (order.fulfillmentMethod === 'Delivery' ? 'orders.delivery' : 'orders.pickup') | t }}
                    {{ order.fulfillmentMethod === 'Delivery' ? '🛵' : '🛍️' }}</span>
                  @if (order.unreadMessages > 0) {
                    <span class="unread" [title]="'orders.unreadMessages' | t">💬 {{ order.unreadMessages }}</span>
                  }
                </div>
                <div class="line2 muted">
                  <b>{{ order.customerName }}</b> · {{ order.itemsSummary }}
                </div>
                <div class="line3 muted">{{ order.createdAt | date: 'MMM d, h:mm a' }}</div>
              </div>
              <div class="col total">{{ order.total | omr }}</div>
            </button>

            @if (expandedId() === order.id) {
              <div class="order__detail">
                @if (detail(); as d) {
                  <div class="detail-grid">
                    <div class="items">
                      @for (item of d.items; track item.id) {
                        <div class="item">
                          <span class="qty">{{ item.quantity }}×</span>
                          <span class="iname">
                            {{ item.productName }}
                            @if (item.variantsText) { <span class="muted">— {{ item.variantsText }}</span> }
                          </span>
                          <span class="iprice">{{ item.lineTotal | omr }}</span>
                        </div>
                      }
                      <div class="totals">
                        <div><span>{{ 'orders.subtotal' | t }}</span><span>{{ d.subtotal | omr }}</span></div>
                        @if (d.deliveryFee > 0) {
                          <div><span>{{ 'orders.deliveryFee' | t }}</span><span>{{ d.deliveryFee | omr }}</span></div>
                        }
                        @if (d.discount > 0) {
                          <div class="disc"><span>{{ 'orders.discount' | t }}</span><span>−{{ d.discount | omr }}</span></div>
                        }
                        <div class="grand"><span>{{ 'orders.total' | t }}</span><span>{{ d.total | omr }}</span></div>
                      </div>
                    </div>
                    <div class="customer">
                      <p><b>{{ 'orders.customer' | t }}:</b> {{ d.customerName }}</p>
                      <p dir="ltr" class="phone-line"><b>{{ 'orders.phone' | t }}:</b> {{ d.customerPhone }}
                        <a class="wa" [href]="waLink(d.customerPhone)" target="_blank" rel="noopener">
                          {{ 'orders.openWhatsApp' | t }}</a></p>
                      @if (d.deliveryAddress) { <p><b>{{ 'orders.address' | t }}:</b> {{ d.deliveryAddress }}
                        @if (d.googleMapsUrl) { <a [href]="d.googleMapsUrl" target="_blank" rel="noopener">📍 {{ 'orders.mapsLink' | t }}</a> }
                      </p> }
                      @if (d.preferredTime) { <p><b>{{ 'orders.preferredTime' | t }}:</b> {{ d.preferredTime }}</p> }
                      @if (d.notes) { <p class="notes"><b>{{ 'orders.notes' | t }}:</b> {{ d.notes }}</p> }
                      @if (d.estimatedReadyAt) {
                        <p><b>{{ 'orders.estimate.promised' | t }}:</b> {{ d.estimatedReadyAt | date: 'h:mm a' }}</p>
                      }
                    </div>
                  </div>
                } @else {
                  <div class="wo-skeleton" style="height: 60px"></div>
                }

                <div class="actions">
                  @if (order.status === 'New') {
                    <select class="estimate" [ngModel]="estimateMinutes()"
                            (ngModelChange)="estimateMinutes.set($event)"
                            [title]="'orders.estimate.label' | t">
                      <option [ngValue]="null">{{ 'orders.estimate.none' | t }}</option>
                      @for (m of estimateChoices; track m) {
                        <option [ngValue]="m">~{{ m }} {{ 'orders.estimate.minutes' | t }}</option>
                      }
                    </select>
                    <button class="wo-btn wo-btn--primary act" type="button"
                            [disabled]="actionBusy()" (click)="setStatus(order, 'Confirmed', estimateMinutes())">
                      {{ 'orders.action.Confirmed' | t }} →
                    </button>
                    <button class="wo-btn wo-btn--danger act" type="button"
                            [disabled]="actionBusy()" (click)="reject(order)">
                      {{ 'orders.action.Rejected' | t }}
                    </button>
                  } @else if (nextStatus(order); as next) {
                    <button class="wo-btn wo-btn--primary act" type="button"
                            [disabled]="actionBusy()" (click)="setStatus(order, next)">
                      {{ 'orders.action.' + next | t }} →
                    </button>
                  }
                  @if (order.status !== 'New' && !isTerminal(order.status)) {
                    <button class="wo-btn wo-btn--danger act" type="button"
                            [disabled]="actionBusy()" (click)="cancel(order)">
                      {{ 'orders.action.Cancelled' | t }}
                    </button>
                  }
                </div>

                <!-- ── Order conversation ─────────────────────────────── -->
                <div class="chat">
                  <h3>💬 {{ 'chat.title' | t }}</h3>
                  @if (chatLoading()) {
                    <div class="wo-skeleton" style="height: 80px"></div>
                  } @else {
                    <wo-chat-thread
                      [messages]="chatMessages()"
                      self="Store"
                      [typing]="peerTyping()"
                      [sending]="chatSending()"
                      [connected]="realtime.connected()"
                      (send)="sendChat($event)"
                      (typingChange)="onTyping($event)" />
                  }
                </div>
              </div>
            }
          </div>
        }
      </div>
    }
  `,
  styles: `
    .head { display: flex; align-items: center; gap: 12px; margin-bottom: 16px;
      h1 { font-size: 22px; } }
    .new-badge { background: var(--wo-primary); color: #fff; border-radius: 999px; padding: 3px 12px;
      font: 700 12.5px var(--wo-font); }
    .live { margin-inline-start: auto; font: 600 12px var(--wo-font); color: var(--wo-muted); }
    .live--on { color: #15803d; }
    .toolbar { display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 16px; align-items: center; }
    .chips { display: flex; gap: 6px; flex-wrap: wrap; }
    .chip { border: 1.5px solid var(--wo-border); background: var(--wo-surface); border-radius: 999px;
      padding: 7px 15px; font: 600 13.5px var(--wo-font); color: var(--wo-ink-soft); cursor: pointer;
      &:hover { border-color: var(--wo-muted); }
      &.chip--on { background: var(--wo-ink); border-color: var(--wo-ink); color: #fff; } }
    .search { flex: 1; min-width: 180px; border: 1.5px solid var(--wo-border); border-radius: 999px;
      padding: 9px 16px; font: 500 14px var(--wo-font); outline: none;
      &:focus { border-color: var(--wo-primary); } }
    .list { display: grid; gap: 10px; }
    .order { overflow: hidden; }
    .order__summary { display: flex; width: 100%; text-align: start; border: none; background: none;
      padding: 14px 18px; cursor: pointer; gap: 12px; align-items: center; font-family: var(--wo-font); }
    .col.main { flex: 1; min-width: 0; }
    .line1 { display: flex; align-items: center; gap: 8px; flex-wrap: wrap;
      .num { font-weight: 800; font-size: 15px; }
      .method { font-size: 12.5px; color: var(--wo-ink-soft); }
      .unread { background: var(--wo-primary); color: #fff; border-radius: 999px;
        padding: 2px 9px; font: 700 11.5px var(--wo-font); } }
    .line2 { font-size: 13.5px; margin-top: 4px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .line3 { font-size: 12px; margin-top: 2px; }
    .col.total { font-weight: 800; font-size: 15.5px; white-space: nowrap; }
    .order__detail { border-top: 1px solid var(--wo-border); padding: 16px 18px; display: grid; gap: 14px; }
    .detail-grid { display: grid; grid-template-columns: 1.2fr 1fr; gap: 18px;
      @media (max-width: 720px) { grid-template-columns: 1fr; } }
    .item { display: flex; gap: 8px; padding: 4px 0; font-size: 14px;
      .qty { font-weight: 700; color: var(--wo-primary-dark); }
      .iname { flex: 1; }
      .iprice { font-weight: 600; } }
    .totals { border-top: 1px dashed var(--wo-border); margin-top: 8px; padding-top: 8px; display: grid; gap: 4px;
      div { display: flex; justify-content: space-between; font-size: 13.5px; color: var(--wo-ink-soft); }
      .disc { color: #15803d; }
      .grand { font-weight: 800; color: var(--wo-ink); font-size: 15px; } }
    .customer { display: grid; gap: 6px; font-size: 14px; align-content: start;
      .notes { background: #fefce8; border-radius: 8px; padding: 8px 10px; }
      .wa { margin-inline-start: 8px; font-weight: 600; color: #128c7e; }
      .phone-line { unicode-bidi: plaintext; } }
    .actions { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; }
    .act { padding: 10px 18px; font-size: 14px; }
    .estimate { border: 1.5px solid var(--wo-border); border-radius: 10px; padding: 9px 12px;
      font: 600 13.5px var(--wo-font); background: var(--wo-surface); color: var(--wo-ink); }
    .chat { border-top: 1px dashed var(--wo-border); padding-top: 12px; display: flex; flex-direction: column;
      h3 { font-size: 14.5px; margin-bottom: 6px; } }
  `,
})
export class OrdersPage {
  private readonly api = inject(StoreApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(TranslationService);
  readonly realtime = inject(OwnerRealtimeService);

  readonly filters = FILTERS;
  readonly estimateChoices = ESTIMATE_CHOICES;
  readonly waLink = whatsAppLink;
  readonly orders = signal<OrderListItemDto[]>([]);
  readonly newCount = signal(0);
  readonly loading = signal(true);
  readonly filter = signal<OrderFilter>('all');
  readonly search = signal('');
  readonly expandedId = signal<string | null>(null);
  readonly detail = signal<OrderDto | null>(null);
  readonly actionBusy = signal(false);
  readonly estimateMinutes = signal<number | null>(null);

  readonly chatMessages = signal<ChatMessageDto[]>([]);
  readonly chatLoading = signal(false);
  readonly chatSending = signal(false);
  readonly peerTyping = signal(false);

  private readonly search$ = new Subject<string>();
  private typingClearTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    const initial = this.route.snapshot.queryParamMap.get('filter') as OrderFilter | null;
    if (initial && FILTERS.includes(initial)) this.filter.set(initial);

    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.load());
    this.load();

    // ── Real-time: the list stays current without any refresh ────────────
    this.realtime.start();

    this.realtime.orderCreated$.pipe(takeUntilDestroyed()).subscribe(() => this.load());

    this.realtime.statusChanged$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      this.orders.update((list) =>
        list.map((o) => (o.id === evt.orderId ? { ...o, status: evt.status } : o)));
      const d = this.detail();
      if (d?.id === evt.orderId) {
        this.detail.set({
          ...d,
          status: evt.status,
          estimatedReadyAt: evt.estimatedReadyAt,
          statusHistory: [...d.statusHistory, evt.entry],
        });
      }
    });

    this.realtime.messageReceived$.pipe(takeUntilDestroyed()).subscribe((message) => {
      if (this.expandedId() === message.orderId) {
        this.appendChatMessage(message);
        if (message.sender === 'Customer') {
          // Viewing the conversation: re-fetch so the server marks it read.
          this.api.getOrderMessages(message.orderId).subscribe((msgs) => this.chatMessages.set(msgs));
        }
      } else if (message.sender === 'Customer') {
        this.orders.update((list) =>
          list.map((o) => (o.id === message.orderId ? { ...o, unreadMessages: o.unreadMessages + 1 } : o)));
      }
    });

    this.realtime.messagesRead$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      if (evt.reader === 'Customer' && this.expandedId() === evt.orderId) {
        this.chatMessages.update((msgs) =>
          msgs.map((m) => (m.sender === 'Store' && !m.readAt ? { ...m, readAt: evt.readAt } : m)));
      }
    });

    this.realtime.typing$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      if (evt.sender === 'Customer' && this.expandedId() === evt.orderId) {
        this.peerTyping.set(evt.isTyping);
        if (this.typingClearTimer) clearTimeout(this.typingClearTimer);
        if (evt.isTyping) this.typingClearTimer = setTimeout(() => this.peerTyping.set(false), 5000);
      }
    });

    this.realtime.reconnected$.pipe(takeUntilDestroyed()).subscribe(() => {
      this.load();
      const expanded = this.expandedId();
      if (expanded) {
        this.realtime.watchOrder(expanded);
        this.api.getOrderMessages(expanded).subscribe((msgs) => this.chatMessages.set(msgs));
      }
    });
  }

  setFilter(filter: OrderFilter): void {
    this.filter.set(filter);
    this.load();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.search$.next(value);
  }

  toggle(order: OrderListItemDto): void {
    const previous = this.expandedId();
    if (previous) this.realtime.unwatchOrder(previous);
    if (previous === order.id) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(order.id);
    this.detail.set(null);
    this.estimateMinutes.set(null);
    this.peerTyping.set(false);
    this.api.getOrder(order.id).subscribe((detail) => this.detail.set(detail));
    this.realtime.watchOrder(order.id);
    this.loadChat(order.id);
  }

  /** Mirrors the backend's natural next step for the quick-action button. */
  nextStatus(order: OrderListItemDto): OrderStatus | null {
    return nextStatusFor(order.status, order.fulfillmentMethod);
  }

  isTerminal(status: OrderStatus): boolean {
    return status === 'Completed' || status === 'Cancelled' || status === 'Rejected';
  }

  setStatus(order: OrderListItemDto, status: OrderStatus, estimatedMinutes?: number | null): void {
    this.actionBusy.set(true);
    this.api.updateOrderStatus(order.id, status, estimatedMinutes).subscribe({
      next: (updated) => {
        this.actionBusy.set(false);
        this.detail.set(updated);
        this.snackBar.open(
          this.i18n.t('orders.statusUpdated', {
            number: updated.orderNumber,
            status: this.i18n.t('orders.status.' + updated.status),
          }),
          undefined,
          { duration: 2500 },
        );
        this.load();
      },
      error: (err) => {
        this.actionBusy.set(false);
        this.snackBar.open(problemMessage(err, this.i18n.t('common.error')), undefined, { duration: 3000 });
      },
    });
  }

  cancel(order: OrderListItemDto): void {
    this.confirmThen(order, 'orders.cancelTitle', 'orders.cancelMessage', 'orders.action.Cancelled', 'Cancelled');
  }

  reject(order: OrderListItemDto): void {
    this.confirmThen(order, 'orders.rejectTitle', 'orders.rejectMessage', 'orders.action.Rejected', 'Rejected');
  }

  // ── Chat ───────────────────────────────────────────────────────────────

  sendChat(body: string): void {
    const orderId = this.expandedId();
    if (!orderId) return;
    this.chatSending.set(true);
    this.api.sendOrderMessage(orderId, body).subscribe({
      next: (message) => {
        this.chatSending.set(false);
        this.appendChatMessage(message);
      },
      error: (err) => {
        this.chatSending.set(false);
        this.snackBar.open(problemMessage(err, this.i18n.t('common.error')), undefined, { duration: 3000 });
      },
    });
  }

  onTyping(isTyping: boolean): void {
    const orderId = this.expandedId();
    if (orderId) this.realtime.sendTyping(orderId, isTyping);
  }

  private loadChat(orderId: string): void {
    this.chatLoading.set(true);
    this.chatMessages.set([]);
    this.api.getOrderMessages(orderId).subscribe({
      next: (messages) => {
        this.chatLoading.set(false);
        this.chatMessages.set(messages);
        // Opening the conversation reads it — clear the row badge.
        this.orders.update((list) =>
          list.map((o) => (o.id === orderId ? { ...o, unreadMessages: 0 } : o)));
      },
      error: () => this.chatLoading.set(false),
    });
  }

  private appendChatMessage(message: ChatMessageDto): void {
    this.chatMessages.update((msgs) =>
      msgs.some((m) => m.id === message.id) ? msgs : [...msgs, message]);
  }

  private confirmThen(
    order: OrderListItemDto, titleKey: string, messageKey: string,
    confirmKey: string, status: OrderStatus,
  ): void {
    this.dialog
      .open(ConfirmDialog, {
        data: {
          titleKey,
          messageKey,
          messageParams: { number: order.orderNumber },
          confirmKey,
          danger: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) this.setStatus(order, status);
      });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getOrders(this.filter(), this.search() || undefined).subscribe({
      next: (page) => {
        this.orders.set(page.items);
        this.newCount.set(page.newCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}

export function nextStatusFor(status: OrderStatus, method: FulfillmentMethod): OrderStatus | null {
  switch (status) {
    case 'New': return 'Confirmed';
    case 'Confirmed': return 'Preparing';
    case 'Preparing': return 'Ready';
    case 'Ready': return method === 'Delivery' ? 'OutForDelivery' : 'Completed';
    case 'OutForDelivery': return 'Completed';
    default: return null;
  }
}
