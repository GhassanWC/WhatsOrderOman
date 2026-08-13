import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HubConnection } from '@microsoft/signalr';
import { PublicStoreService } from '../../core/services/public-store.service';
import {
  ChatMessageDto, MessagesReadEvent, OrderStatus, OrderStatusChangedEvent,
  PublicOrderStatusDto, TypingEvent,
} from '../../core/models/api-types';
import { buildCustomerConnection } from '../../core/realtime/realtime.service';
import { StatusChip } from '../../shared/components';
import { ChatThread } from '../../shared/chat-thread';
import { OmrPricePipe, TranslatePipe } from '../../shared/pipes';
import { whatsAppLink } from '../../shared/whatsapp';
import { TranslationService } from '../../core/i18n/translation.service';

export interface TimelineStep {
  status: OrderStatus;
  done: boolean;
  current: boolean;
  at: string | null;
}

/**
 * Vertical progress derived from the persisted history — no client guesswork.
 * Cancelled/Rejected render as the reached steps plus a red terminal entry.
 */
export function buildTimeline(
  order: Pick<PublicOrderStatusDto, 'status' | 'fulfillmentMethod' | 'statusHistory'>,
): TimelineStep[] {
  const flow: OrderStatus[] = order.fulfillmentMethod === 'Delivery'
    ? ['New', 'Confirmed', 'Preparing', 'Ready', 'OutForDelivery', 'Completed']
    : ['New', 'Confirmed', 'Preparing', 'Ready', 'Completed'];

  const reachedAt = new Map<OrderStatus, string>();
  for (const entry of order.statusHistory) reachedAt.set(entry.newStatus, entry.changedAt);

  if (order.status === 'Cancelled' || order.status === 'Rejected') {
    const reached = flow.filter((s) => reachedAt.has(s));
    return [...reached, order.status].map((status) => ({
      status,
      done: true,
      current: status === order.status,
      at: reachedAt.get(status) ?? null,
    }));
  }

  const currentIndex = flow.indexOf(order.status);
  return flow.map((status, index) => ({
    status,
    done: index <= currentIndex,
    current: index === currentIndex,
    at: reachedAt.get(status) ?? null,
  }));
}

/**
 * Customer order tracking: confirmation hero + live status timeline + chat with the
 * store. Everything updates in real time over SignalR, but the page never depends on
 * it — REST carries the state and ⟳ refetches on demand or after a reconnect.
 */
@Component({
  selector: 'wo-order-confirmation-page',
  imports: [DatePipe, RouterLink, StatusChip, ChatThread, OmrPricePipe, TranslatePipe],
  template: `
    <div class="page container">
      <div class="card wo-card">
        <div class="party">🎉</div>
        <h1>{{ 'confirmation.title' | t }}</h1>
        <p class="muted">{{ 'confirmation.subtitle' | t }}</p>
        <div class="number">#{{ orderNumber() }}</div>
        <p class="note muted">{{ 'confirmation.trackNote' | t }}</p>

        @if (order(); as o) {
          <div class="status-row">
            <span>{{ 'confirmation.status' | t }}:</span>
            <wo-status-chip [status]="o.status" />
            <span class="live" [class.live--on]="connected()">●</span>
            <button type="button" class="refresh" (click)="refresh()" [disabled]="refreshing()">⟳</button>
          </div>

          @if (o.estimatedReadyAt && !isTerminal(o.status)) {
            <p class="eta">⏱️ {{ 'confirmation.estimatedReady' | t }} ≈ {{ o.estimatedReadyAt | date: 'h:mm a' }}</p>
          }

          <!-- ── Status timeline ─────────────────────────────────────── -->
          <div class="timeline">
            @for (step of timeline(); track step.status) {
              <div class="step" [class.step--done]="step.done" [class.step--current]="step.current">
                <span class="dot"></span>
                <span class="label">{{ 'orders.status.' + step.status | t }}</span>
                @if (step.at) { <span class="when">{{ step.at | date: 'h:mm a' }}</span> }
              </div>
            }
          </div>

          <ul class="items">
            @for (item of o.items; track item.id) {
              <li>
                <span class="qty">{{ item.quantity }}×</span>
                <span class="name">{{ item.productName }}
                  @if (item.variantsText) { <small class="muted">{{ item.variantsText }}</small> }
                </span>
                <span class="price">{{ item.lineTotal | omr }}</span>
              </li>
            }
          </ul>
          <div class="totals">
            @if (o.deliveryFee > 0) {
              <div><span>{{ 'cart.deliveryFee' | t }}</span><span>{{ o.deliveryFee | omr }}</span></div>
            }
            <div class="grand"><span>{{ 'cart.total' | t }}</span><span>{{ o.total | omr }}</span></div>
          </div>
        }

        <div class="actions">
          <a class="wo-btn wo-btn--ghost" [routerLink]="['/', slug()]">{{ 'confirmation.backToStore' | t }}</a>
        </div>
      </div>

      <!-- ── Chat with the store ───────────────────────────────────────── -->
      @if (order()) {
        <div class="card wo-card chat-card">
          <div class="chat-head">
            <h2>💬 {{ 'confirmation.chatWithStore' | t }}</h2>
            @if (storeWhatsApp()) {
              <a class="wa-alt" [href]="whatsAppAlt()" target="_blank" rel="noopener">WhatsApp ↗</a>
            }
          </div>
          <wo-chat-thread
            [messages]="messages()"
            self="Customer"
            [typing]="storeTyping()"
            [sending]="sending()"
            [connected]="connected()"
            (send)="sendMessage($event)"
            (typingChange)="onTyping($event)" />
        </div>
      }
    </div>
  `,
  styles: `
    .page { padding-block: 32px 60px; max-width: 560px; display: grid; gap: 14px; }
    .card { padding: 32px 26px; text-align: center; display: grid; gap: 10px; justify-items: center; }
    .party { font-size: 48px; }
    h1 { font-size: 24px; }
    .number { font: 800 30px var(--wo-font); color: var(--wo-primary-dark); letter-spacing: .01em; direction: ltr; }
    .note { font-size: 14px; max-width: 380px; }
    .status-row { display: flex; align-items: center; gap: 10px; margin-top: 6px; font-size: 14px;
      .live { color: var(--wo-muted); font-size: 11px; }
      .live--on { color: #22c55e; }
      .refresh { border: none; background: var(--wo-bg); width: 30px; height: 30px; border-radius: 50%;
        cursor: pointer; font-size: 15px; &:hover { background: var(--wo-primary-soft); } } }
    .eta { font: 600 13.5px var(--wo-font); color: var(--wo-primary-dark); }
    .timeline { width: 100%; display: grid; gap: 0; text-align: start; margin-top: 6px; }
    .step { display: flex; align-items: baseline; gap: 10px; position: relative; padding: 7px 0 7px;
      padding-inline-start: 22px; color: var(--wo-muted); font-size: 13.5px;
      .dot { position: absolute; inset-inline-start: 4px; top: 12px; width: 9px; height: 9px;
        border-radius: 50%; background: var(--wo-border); }
      &::before { content: ''; position: absolute; inset-inline-start: 8px; top: 22px; bottom: -6px;
        width: 1.5px; background: var(--wo-border); }
      &:last-child::before { display: none; }
      .label { font-weight: 600; }
      .when { margin-inline-start: auto; font-size: 11.5px; direction: ltr; } }
    .step--done { color: var(--wo-ink);
      .dot { background: var(--wo-primary); } }
    .step--current .label { color: var(--wo-primary-dark); font-weight: 800; }
    .items { list-style: none; margin: 10px 0 0; padding: 14px 0 0; border-top: 1px dashed var(--wo-border);
      width: 100%; display: grid; gap: 8px; text-align: start;
      li { display: flex; gap: 8px; font-size: 14px; align-items: baseline; }
      .qty { font-weight: 700; color: var(--wo-primary-dark); }
      .name { flex: 1; small { display: block; font-size: 12px; } }
      .price { font-weight: 600; } }
    .totals { width: 100%; display: grid; gap: 4px; border-top: 1px dashed var(--wo-border); padding-top: 10px;
      div { display: flex; justify-content: space-between; font-size: 14px; color: var(--wo-ink-soft); }
      .grand { font-weight: 800; color: var(--wo-ink); font-size: 16px; } }
    .actions { display: grid; gap: 10px; width: 100%; margin-top: 12px; }
    .chat-card { text-align: start; display: flex; flex-direction: column; padding: 20px; }
    .chat-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 6px;
      h2 { font-size: 16.5px; }
      .wa-alt { font: 600 12.5px var(--wo-font); color: #128c7e; } }
  `,
})
export class OrderConfirmationPage {
  readonly slug = input.required<string>();
  readonly orderNumber = input.required<string>();

  private readonly api = inject(PublicStoreService);
  private readonly route = inject(ActivatedRoute);
  private readonly i18n = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly order = signal<PublicOrderStatusDto | null>(null);
  readonly refreshing = signal(false);
  readonly storeWhatsApp = signal<string | null>(null);
  readonly messages = signal<ChatMessageDto[]>([]);
  readonly sending = signal(false);
  readonly storeTyping = signal(false);
  readonly connected = signal(false);

  /** Secondary channel, shown only when the store connected WhatsApp. */
  readonly whatsAppAlt = computed(() =>
    whatsAppLink(this.storeWhatsApp(), this.i18n.t('confirmation.chatPrefill', { number: this.orderNumber() })));

  private phone = '';
  private hub: HubConnection | null = null;
  private typingClearTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => void this.hub?.stop());

    effect(() => {
      const slug = this.slug();
      const number = this.orderNumber();
      this.phone = this.route.snapshot.queryParamMap.get('phone') ?? '';
      if (slug && number && this.phone) {
        this.refresh();
        this.loadMessages();
        this.startHub();
      }
      if (!this.storeWhatsApp()) {
        this.api.getStore(slug).subscribe((store) =>
          this.storeWhatsApp.set(store.whatsAppNumber || null));
      }
    });
  }

  readonly timeline = computed<TimelineStep[]>(() => {
    const order = this.order();
    return order ? buildTimeline(order) : [];
  });

  isTerminal(status: OrderStatus): boolean {
    return status === 'Completed' || status === 'Cancelled' || status === 'Rejected';
  }

  refresh(): void {
    if (!this.phone) return;
    this.refreshing.set(true);
    this.api.getOrderStatus(this.slug(), this.orderNumber(), this.phone).subscribe({
      next: (order) => {
        this.order.set(order);
        this.refreshing.set(false);
      },
      error: () => this.refreshing.set(false),
    });
  }

  sendMessage(body: string): void {
    this.sending.set(true);
    this.api.sendOrderMessage(this.slug(), this.orderNumber(), this.phone, body).subscribe({
      next: (message) => {
        this.sending.set(false);
        this.append(message);
      },
      error: () => this.sending.set(false),
    });
  }

  onTyping(isTyping: boolean): void {
    const orderId = this.order()?.orderId;
    if (orderId && this.hub) void this.hub.invoke('Typing', orderId, isTyping).catch(() => undefined);
  }

  private loadMessages(): void {
    // GET also marks the store's messages as read (their ✓✓).
    this.api.getOrderMessages(this.slug(), this.orderNumber(), this.phone).subscribe({
      next: (messages) => this.messages.set(messages),
      error: () => undefined,
    });
  }

  private append(message: ChatMessageDto): void {
    this.messages.update((msgs) =>
      msgs.some((m) => m.id === message.id) ? msgs : [...msgs, message]);
  }

  private startHub(): void {
    if (this.hub) return;
    const hub = buildCustomerConnection(this.slug(), this.orderNumber(), this.phone);

    hub.on('orderStatusChanged', (evt: OrderStatusChangedEvent) => {
      const order = this.order();
      if (!order || evt.orderNumber !== order.orderNumber) return;
      this.order.set({
        ...order,
        status: evt.status,
        estimatedReadyAt: evt.estimatedReadyAt,
        statusHistory: [...order.statusHistory, evt.entry],
      });
    });

    hub.on('messageReceived', (message: ChatMessageDto) => {
      this.append(message);
      // We are looking at the conversation — let the server mark it read.
      if (message.sender === 'Store') this.loadMessages();
    });

    hub.on('messagesRead', (evt: MessagesReadEvent) => {
      if (evt.reader !== 'Store') return;
      this.messages.update((msgs) =>
        msgs.map((m) => (m.sender === 'Customer' && !m.readAt ? { ...m, readAt: evt.readAt } : m)));
    });

    hub.on('typing', (evt: TypingEvent) => {
      if (evt.sender !== 'Store') return;
      this.storeTyping.set(evt.isTyping);
      if (this.typingClearTimer) clearTimeout(this.typingClearTimer);
      if (evt.isTyping) this.typingClearTimer = setTimeout(() => this.storeTyping.set(false), 5000);
    });

    hub.onreconnected(() => {
      this.connected.set(true);
      this.refresh();
      this.loadMessages();
    });
    hub.onreconnecting(() => this.connected.set(false));
    hub.onclose(() => this.connected.set(false));

    this.hub = hub;
    hub.start()
      .then(() => this.connected.set(true))
      .catch(() => this.connected.set(false));
  }
}
