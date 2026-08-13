import { Injectable, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  IRetryPolicy,
  RetryContext,
} from '@microsoft/signalr';
import { AuthService } from '../services/auth.service';
import {
  ChatMessageDto, MessagesReadEvent, NotificationDto, OrderListItemDto,
  OrderStatusChangedEvent, TypingEvent,
} from '../models/api-types';

/** Exponential backoff that never gives up — orders must keep flowing in. */
const RETRY_FOREVER: IRetryPolicy = {
  nextRetryDelayInMilliseconds: (context: RetryContext) =>
    Math.min(30_000, 1000 * 2 ** Math.min(context.previousRetryCount, 5)),
};

/**
 * The customer side connects anonymously, proving order ownership with the same
 * slug + orderNumber + phone triple the public REST endpoints use.
 */
export function buildCustomerConnection(slug: string, orderNumber: string, phone: string): HubConnection {
  const query = new URLSearchParams({ slug, orderNumber, phone }).toString();
  return new HubConnectionBuilder()
    .withUrl(`/hubs/orders?${query}`)
    .withAutomaticReconnect(RETRY_FOREVER)
    .build();
}

/**
 * Owner-side singleton connection for the dashboard: joins store:{id} on the server
 * and re-emits hub events as streams. Started by the dashboard shell; safe to call
 * start() many times. Orders never depend on this being connected — REST is the
 * source of truth and `reconnected$` tells pages to re-sync after a gap.
 */
@Injectable({ providedIn: 'root' })
export class OwnerRealtimeService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;

  readonly connected = signal(false);

  readonly orderCreated$ = new Subject<OrderListItemDto>();
  readonly statusChanged$ = new Subject<OrderStatusChangedEvent>();
  readonly messageReceived$ = new Subject<ChatMessageDto>();
  readonly messagesRead$ = new Subject<MessagesReadEvent>();
  readonly typing$ = new Subject<TypingEvent>();
  readonly notification$ = new Subject<NotificationDto>();
  /** Fires after an automatic reconnect — listeners should re-fetch their snapshots. */
  readonly reconnected$ = new Subject<void>();

  start(): void {
    if (this.connection) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/orders', {
        // SignalR cannot send Authorization headers over WebSockets; the factory is
        // re-invoked on every (re)connect so a refreshed token is picked up.
        accessTokenFactory: () => this.auth.accessToken() ?? '',
      })
      .withAutomaticReconnect(RETRY_FOREVER)
      .build();

    connection.on('orderCreated', (order: OrderListItemDto) => this.orderCreated$.next(order));
    connection.on('orderStatusChanged', (evt: OrderStatusChangedEvent) => this.statusChanged$.next(evt));
    connection.on('messageReceived', (message: ChatMessageDto) => this.messageReceived$.next(message));
    connection.on('messagesRead', (evt: MessagesReadEvent) => this.messagesRead$.next(evt));
    connection.on('typing', (evt: TypingEvent) => this.typing$.next(evt));
    connection.on('notification', (notification: NotificationDto) => this.notification$.next(notification));

    connection.onreconnected(() => {
      this.connected.set(true);
      this.reconnected$.next();
    });
    connection.onreconnecting(() => this.connected.set(false));
    connection.onclose(() => {
      this.connected.set(false);
      // Closed for good (e.g. expired token at connect time): retry from scratch.
      setTimeout(() => this.restart(), 10_000);
    });

    this.connection = connection;
    void this.tryStart();
  }

  stop(): void {
    const connection = this.connection;
    this.connection = null;
    this.connected.set(false);
    void connection?.stop();
  }

  /** Owner opened an order's chat: join its group for typing/read receipts. */
  watchOrder(orderId: string): void {
    void this.invokeSafe('WatchOrder', orderId);
  }

  unwatchOrder(orderId: string): void {
    void this.invokeSafe('UnwatchOrder', orderId);
  }

  sendTyping(orderId: string, isTyping: boolean): void {
    void this.invokeSafe('Typing', orderId, isTyping);
  }

  private async tryStart(): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Disconnected) return;
    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      this.connected.set(false);
      setTimeout(() => this.tryStart(), 5000);
    }
  }

  private restart(): void {
    if (!this.connection) return; // stopped deliberately
    void this.tryStart();
  }

  private async invokeSafe(method: string, ...args: unknown[]): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Connected) return;
    try {
      await this.connection.invoke(method, ...args);
    } catch {
      // Group membership calls are best-effort; a reconnect re-establishes state.
    }
  }
}

/**
 * Buyer-side singleton connection: the server joins it to buyer:{userId}, so order
 * status changes, store replies and account notifications arrive live anywhere in
 * the marketplace. Conversations are joined on demand via WatchMyOrder. Same rules
 * as the owner connection: REST is the source of truth, realtime is an accelerator.
 */
@Injectable({ providedIn: 'root' })
export class BuyerRealtimeService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;

  readonly connected = signal(false);

  readonly statusChanged$ = new Subject<OrderStatusChangedEvent>();
  readonly messageReceived$ = new Subject<ChatMessageDto>();
  readonly messagesRead$ = new Subject<MessagesReadEvent>();
  readonly typing$ = new Subject<TypingEvent>();
  readonly notification$ = new Subject<NotificationDto>();
  readonly reconnected$ = new Subject<void>();

  start(): void {
    if (this.connection || !this.auth.isAuthenticated()) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/orders', {
        accessTokenFactory: () => this.auth.accessToken() ?? '',
      })
      .withAutomaticReconnect(RETRY_FOREVER)
      .build();

    connection.on('orderStatusChanged', (evt: OrderStatusChangedEvent) => this.statusChanged$.next(evt));
    connection.on('messageReceived', (message: ChatMessageDto) => this.messageReceived$.next(message));
    connection.on('messagesRead', (evt: MessagesReadEvent) => this.messagesRead$.next(evt));
    connection.on('typing', (evt: TypingEvent) => this.typing$.next(evt));
    connection.on('notification', (notification: NotificationDto) => this.notification$.next(notification));

    connection.onreconnected(() => {
      this.connected.set(true);
      this.reconnected$.next();
    });
    connection.onreconnecting(() => this.connected.set(false));
    connection.onclose(() => {
      this.connected.set(false);
      setTimeout(() => this.restart(), 10_000);
    });

    this.connection = connection;
    void this.tryStart();
  }

  stop(): void {
    const connection = this.connection;
    this.connection = null;
    this.connected.set(false);
    void connection?.stop();
  }

  /** Buyer opened one of their conversations — join its order group. */
  watchOrder(orderId: string): void {
    void this.invokeSafe('WatchMyOrder', orderId);
  }

  unwatchOrder(orderId: string): void {
    void this.invokeSafe('UnwatchMyOrder', orderId);
  }

  sendTyping(orderId: string, isTyping: boolean): void {
    void this.invokeSafe('Typing', orderId, isTyping);
  }

  private async tryStart(): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Disconnected) return;
    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      this.connected.set(false);
      setTimeout(() => this.tryStart(), 5000);
    }
  }

  private restart(): void {
    if (!this.connection) return;
    void this.tryStart();
  }

  private async invokeSafe(method: string, ...args: unknown[]): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Connected) return;
    try {
      await this.connection.invoke(method, ...args);
    } catch {
      // Best-effort; a reconnect re-establishes group state.
    }
  }
}
