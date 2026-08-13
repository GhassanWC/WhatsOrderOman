import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountApiService } from '../../core/services/account-api.service';
import { BuyerRealtimeService } from '../../core/realtime/realtime.service';
import { BuyerConversationDto, ChatMessageDto } from '../../core/models/api-types';
import { ChatThread } from '../../shared/chat-thread';
import { EmptyState } from '../../shared/components';
import { BilingualPipe, TranslatePipe } from '../../shared/pipes';
import { TranslationService } from '../../core/i18n/translation.service';

/**
 * The buyer's messaging inbox: conversations (one per order) on one side, the
 * live thread on the other. On mobile the two swap with a back button.
 */
@Component({
  selector: 'wo-account-messages',
  imports: [DatePipe, RouterLink, TranslatePipe, BilingualPipe, ChatThread, EmptyState],
  template: `
    <h1>{{ 'account.nav.messages' | t }}</h1>

    <div class="split" [class.thread-open]="selected() !== null">
      <!-- ── Inbox ───────────────────────────────────────────────────── -->
      <aside class="inbox wo-card">
        <input
          class="search"
          type="search"
          [placeholder]="'account.searchConversations' | t"
          (input)="search$.next($any($event.target).value)"
        />
        @if (loading()) {
          <div class="wo-skeleton" style="height: 64px; margin: 8px"></div>
          <div class="wo-skeleton" style="height: 64px; margin: 8px"></div>
        } @else if (conversations().length === 0) {
          <wo-empty-state emoji="💬" messageKey="account.noConversations" />
        } @else {
          @for (convo of conversations(); track convo.orderId) {
            <button type="button" class="convo" [class.on]="selected()?.orderId === convo.orderId"
                    (click)="open(convo)">
              <div class="logo">
                @if (convo.storeLogoUrl; as logo) {
                  <img [src]="logo" alt="" loading="lazy" />
                } @else {
                  <span>{{ convo.storeName.charAt(0) }}</span>
                }
              </div>
              <div class="mid">
                <div class="row1">
                  <b>{{ convo.storeName | bilingual: convo.storeNameAr }}</b>
                  <small>{{ convo.lastMessageAt | date: 'shortTime' }}</small>
                </div>
                <div class="row2">
                  <small class="muted">#{{ convo.orderNumber }} · {{ lastMessagePreview(convo) }}</small>
                  @if (convo.unreadCount > 0) {
                    <span class="unread">{{ convo.unreadCount }}</span>
                  }
                </div>
              </div>
            </button>
          }
        }
      </aside>

      <!-- ── Thread ──────────────────────────────────────────────────── -->
      <section class="thread wo-card">
        @if (selected(); as convo) {
          <header class="thread-head">
            <button type="button" class="back" (click)="close()">←</button>
            <div class="mid">
              <b>{{ convo.storeName | bilingual: convo.storeNameAr }}</b>
              <a [routerLink]="['/account/orders', convo.orderId]" class="muted">#{{ convo.orderNumber }}</a>
            </div>
            <a [routerLink]="['/', convo.storeSlug]" class="wo-btn wo-btn--ghost mini">
              {{ 'account.viewStore' | t }}
            </a>
          </header>
          <wo-chat-thread
            [messages]="messages()"
            self="Customer"
            [typing]="storeTyping()"
            [sending]="sending()"
            [connected]="realtime.connected()"
            (send)="send($event)"
            (typingChange)="realtime.sendTyping(convo.orderId, $event)"
          />
        } @else {
          <div class="pick muted">
            <span>💬</span>
            <p>{{ 'account.pickConversation' | t }}</p>
          </div>
        }
      </section>
    </div>
  `,
  styles: `
    h1 { margin: 0 0 14px; font-size: 22px; font-weight: 800; }
    .split { display: grid; grid-template-columns: 320px minmax(0, 1fr); gap: 14px; align-items: start; }
    .inbox { padding: 10px; max-height: 72dvh; overflow-y: auto; }
    .search { width: calc(100% - 8px); margin: 4px; border: 1px solid var(--wo-border);
      border-radius: 999px; padding: 9px 14px; font: inherit; outline: none;
      &:focus { border-color: var(--wo-primary); } }
    .convo { display: flex; gap: 10px; width: 100%; border: none; background: transparent;
      padding: 10px; border-radius: 10px; cursor: pointer; text-align: start; font-family: var(--wo-font);
      &:hover { background: var(--wo-bg); }
      &.on { background: var(--wo-primary-soft); } }
    .logo { width: 42px; height: 42px; border-radius: 11px; overflow: hidden; flex-shrink: 0;
      background: var(--wo-primary-soft); color: var(--wo-primary-dark);
      display: grid; place-items: center; font-weight: 800; }
    .logo img { width: 100%; height: 100%; object-fit: cover; }
    .mid { flex: 1; min-width: 0; display: grid; gap: 2px; }
    .row1 { display: flex; justify-content: space-between; align-items: baseline; gap: 8px;
      b { font-size: 14px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
      small { color: var(--wo-muted); font-size: 11.5px; white-space: nowrap; } }
    .row2 { display: flex; justify-content: space-between; align-items: center; gap: 8px;
      small { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: 12.5px; } }
    .unread { background: var(--wo-danger); color: #fff; border-radius: 999px; min-width: 18px;
      height: 18px; padding-inline: 5px; font-size: 11px; font-weight: 700;
      display: inline-grid; place-items: center; flex-shrink: 0; }
    .thread { padding: 12px 14px; min-height: 420px; display: grid; grid-template-rows: auto 1fr; }
    .thread-head { display: flex; align-items: center; gap: 10px; padding-bottom: 10px;
      border-bottom: 1px solid var(--wo-border); margin-bottom: 8px;
      .mid { display: grid; gap: 0;
        b { font-size: 15px; }
        a { font-size: 12.5px; text-decoration: none; } } }
    .back { display: none; border: none; background: transparent; font-size: 20px; cursor: pointer; }
    .mini { padding: 6px 12px; font-size: 12.5px; margin-inline-start: auto; }
    .pick { display: grid; place-content: center; gap: 6px; text-align: center; min-height: 320px;
      span { font-size: 40px; }
      p { margin: 0; } }

    @media (max-width: 860px) {
      .split { grid-template-columns: 1fr; }
      .thread { display: none; }
      .split.thread-open .inbox { display: none; }
      .split.thread-open .thread { display: grid; }
      .back { display: block; }
    }
  `,
})
export class AccountMessagesPage {
  private readonly api = inject(AccountApiService);
  protected readonly realtime = inject(BuyerRealtimeService);
  protected readonly i18n = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly search$ = new Subject<string>();
  readonly loading = signal(true);
  readonly conversations = signal<BuyerConversationDto[]>([]);
  readonly selected = signal<BuyerConversationDto | null>(null);
  readonly messages = signal<ChatMessageDto[]>([]);
  readonly sending = signal(false);
  readonly storeTyping = signal(false);

  private typingTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.realtime.start();
    this.reload();

    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((term) => this.reload(term));

    this.realtime.messageReceived$.pipe(takeUntilDestroyed()).subscribe((message) => {
      const current = this.selected();
      if (current && message.orderId === current.orderId) {
        this.messages.update((list) =>
          list.some((m) => m.id === message.id) ? list : [...list, message]);
        this.storeTyping.set(false);
      }
      this.reload(); // refresh previews/unread counts
    });
    this.realtime.messagesRead$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      const current = this.selected();
      if (!current || evt.orderId !== current.orderId || evt.reader !== 'Store') return;
      this.messages.update((list) =>
        list.map((m) => (m.sender === 'Customer' && !m.readAt ? { ...m, readAt: evt.readAt } : m)));
    });
    this.realtime.typing$.pipe(takeUntilDestroyed()).subscribe((evt) => {
      const current = this.selected();
      if (!current || evt.orderId !== current.orderId || evt.sender !== 'Store') return;
      this.storeTyping.set(evt.isTyping);
      if (this.typingTimer) clearTimeout(this.typingTimer);
      if (evt.isTyping) this.typingTimer = setTimeout(() => this.storeTyping.set(false), 4000);
    });
    this.realtime.reconnected$.pipe(takeUntilDestroyed()).subscribe(() => {
      this.reload();
      const current = this.selected();
      if (current) {
        this.realtime.watchOrder(current.orderId);
        this.loadThread(current.orderId);
      }
    });

    this.destroyRef.onDestroy(() => {
      const current = this.selected();
      if (current) this.realtime.unwatchOrder(current.orderId);
    });
  }

  lastMessagePreview(convo: BuyerConversationDto): string {
    // System messages are stored as machine tokens — localize them for the preview.
    return convo.lastMessage.startsWith('status:')
      ? this.i18n.t('orders.status.' + convo.lastMessage.slice(7))
      : convo.lastMessage;
  }

  open(convo: BuyerConversationDto): void {
    const previous = this.selected();
    if (previous && previous.orderId !== convo.orderId) {
      this.realtime.unwatchOrder(previous.orderId);
    }
    this.selected.set(convo);
    this.messages.set([]);
    this.realtime.watchOrder(convo.orderId);
    this.loadThread(convo.orderId);
  }

  close(): void {
    const current = this.selected();
    if (current) this.realtime.unwatchOrder(current.orderId);
    this.selected.set(null);
    this.reload();
  }

  send(body: string): void {
    const convo = this.selected();
    if (!convo || this.sending()) return;
    this.sending.set(true);
    this.api.sendMessage(convo.orderId, body).subscribe({
      next: (message) => {
        this.messages.update((list) =>
          list.some((m) => m.id === message.id) ? list : [...list, message]);
        this.sending.set(false);
      },
      error: () => this.sending.set(false),
    });
  }

  private reload(search?: string): void {
    this.api.conversations(search).subscribe({
      next: (conversations) => {
        this.conversations.set(conversations);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private loadThread(orderId: string): void {
    this.api.messages(orderId).subscribe({
      next: (messages) => {
        this.messages.set(messages);
        // Reading clears the unread badge — refresh the inbox counts.
        this.conversations.update((list) =>
          list.map((c) => (c.orderId === orderId ? { ...c, unreadCount: 0 } : c)));
      },
      error: () => undefined,
    });
  }
}
