import {
  Component, ElementRef, effect, input, output, signal, viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatMessageDto, ChatSender } from '../core/models/api-types';
import { TranslatePipe } from './pipes';

/**
 * Order conversation thread — mobile-first bubbles, shared by the owner dashboard
 * (self = Store) and the customer tracking page (self = Customer).
 * System messages arrive as machine tokens ("status:Confirmed") and are rendered
 * through i18n so each side reads them in its own language.
 */
@Component({
  selector: 'wo-chat-thread',
  imports: [DatePipe, FormsModule, TranslatePipe],
  template: `
    @if (!connected()) {
      <div class="reconnect">⚡ {{ 'chat.reconnecting' | t }}</div>
    }

    <div #scroller class="thread" [class.thread--empty]="messages().length === 0">
      @if (messages().length === 0) {
        <p class="empty">{{ 'chat.empty' | t }}</p>
      }
      @for (m of messages(); track m.id) {
        @if (m.sender === 'System') {
          <div class="system">{{ systemKey(m.body) | t }}</div>
        } @else {
          <div class="bubble" [class.bubble--mine]="m.sender === self()">
            <span class="body">{{ m.body }}</span>
            <span class="meta">
              {{ m.sentAt | date: 'h:mm a' }}
              @if (m.sender === self()) {
                <span class="ticks" [class.ticks--read]="m.readAt">{{ m.readAt ? '✓✓' : '✓' }}</span>
              }
            </span>
          </div>
        }
      }
      @if (typing()) {
        <div class="bubble bubble--typing" aria-live="polite">
          <span class="dots"><i></i><i></i><i></i></span>
        </div>
      }
    </div>

    <form class="composer" (submit)="submit($event)">
      <input
        type="text"
        [placeholder]="'chat.placeholder' | t"
        [ngModel]="draft()"
        (ngModelChange)="onDraftChange($event)"
        name="message"
        maxlength="1000"
        autocomplete="off"
        [disabled]="sending()"
      />
      <button type="submit" class="send" [disabled]="sending() || !draft().trim()">
        {{ 'chat.send' | t }}
      </button>
    </form>
  `,
  styles: `
    :host { display: flex; flex-direction: column; min-height: 0; }
    .reconnect { background: #fef3c7; color: #b45309; font: 600 12.5px var(--wo-font);
      padding: 6px 12px; border-radius: 8px; margin-bottom: 8px; text-align: center; }
    .thread { flex: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 6px;
      padding: 10px 4px; min-height: 120px; max-height: 340px; scroll-behavior: smooth; }
    .thread--empty { justify-content: center; }
    .empty { text-align: center; color: var(--wo-muted); font-size: 13.5px; }
    .system { align-self: center; background: var(--wo-bg); color: var(--wo-ink-soft);
      font: 600 12px var(--wo-font); border-radius: 999px; padding: 4px 14px; margin-block: 4px; }
    .bubble { max-width: 78%; align-self: flex-start; background: var(--wo-surface);
      border: 1px solid var(--wo-border); border-radius: 14px;
      border-start-start-radius: 4px; padding: 8px 12px 6px;
      display: flex; flex-direction: column; gap: 2px;
      .body { font-size: 14px; white-space: pre-wrap; word-break: break-word; }
      .meta { font-size: 10.5px; color: var(--wo-muted); align-self: flex-end;
        display: inline-flex; gap: 4px; align-items: center; direction: ltr; } }
    .bubble--mine { align-self: flex-end; background: var(--wo-primary-soft);
      border-color: transparent; border-radius: 14px; border-start-end-radius: 4px;
      .ticks { color: var(--wo-muted); }
      .ticks--read { color: var(--wo-primary-dark); font-weight: 700; } }
    .bubble--typing { padding: 10px 14px; }
    .dots { display: inline-flex; gap: 4px;
      i { width: 6px; height: 6px; border-radius: 50%; background: var(--wo-muted);
        animation: wo-blink 1.2s infinite;
        &:nth-child(2) { animation-delay: .2s; }
        &:nth-child(3) { animation-delay: .4s; } } }
    @keyframes wo-blink { 0%, 60%, 100% { opacity: .25; } 30% { opacity: 1; } }
    .composer { display: flex; gap: 8px; margin-top: 10px;
      input { flex: 1; border: 1.5px solid var(--wo-border); border-radius: 999px;
        padding: 10px 16px; font: 500 14px var(--wo-font); outline: none;
        &:focus { border-color: var(--wo-primary); } }
      .send { border: none; background: var(--wo-primary); color: #fff; border-radius: 999px;
        padding: 0 20px; font: 700 13.5px var(--wo-font); cursor: pointer;
        &:disabled { opacity: .5; cursor: default; } } }
  `,
})
export class ChatThread {
  readonly messages = input.required<ChatMessageDto[]>();
  readonly self = input.required<ChatSender>();
  readonly typing = input(false);
  readonly sending = input(false);
  readonly connected = input(true);

  readonly send = output<string>();
  readonly typingChange = output<boolean>();

  readonly draft = signal('');

  private readonly scroller = viewChild.required<ElementRef<HTMLDivElement>>('scroller');
  private typingTimer: ReturnType<typeof setTimeout> | null = null;
  private isTypingSent = false;

  constructor() {
    // Follow the conversation: stick to the bottom whenever messages change.
    effect(() => {
      this.messages();
      this.typing();
      const el = this.scroller().nativeElement;
      setTimeout(() => el.scrollTo({ top: el.scrollHeight }));
    });
  }

  systemKey(body: string): string {
    const status = body.startsWith('status:') ? body.slice('status:'.length) : null;
    return status ? `chat.system.${status}` : 'chat.system.unknown';
  }

  onDraftChange(value: string): void {
    this.draft.set(value);
    if (!this.isTypingSent) {
      this.isTypingSent = true;
      this.typingChange.emit(true);
    }
    if (this.typingTimer) clearTimeout(this.typingTimer);
    this.typingTimer = setTimeout(() => {
      this.isTypingSent = false;
      this.typingChange.emit(false);
    }, 2500);
  }

  submit(event: Event): void {
    event.preventDefault();
    const body = this.draft().trim();
    if (!body || this.sending()) return;
    this.draft.set('');
    if (this.typingTimer) clearTimeout(this.typingTimer);
    if (this.isTypingSent) {
      this.isTypingSent = false;
      this.typingChange.emit(false);
    }
    this.send.emit(body);
  }
}
