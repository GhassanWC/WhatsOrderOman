import { Component, computed, inject, input } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { OrderStatus, ApiProblem } from '../core/models/api-types';
import { TranslationService } from '../core/i18n/translation.service';
import { TranslatePipe } from './pipes';

/** Colored order-status chip. */
@Component({
  selector: 'wo-status-chip',
  imports: [TranslatePipe],
  template: `<span class="wo-chip wo-chip--{{ status().toLowerCase() }}">{{ key() | t }}</span>`,
})
export class StatusChip {
  readonly status = input.required<OrderStatus>();
  readonly key = computed(() => `orders.status.${this.status()}`);
}

/** EN ⇄ العربية toggle. */
@Component({
  selector: 'wo-lang-switcher',
  template: `
    <button type="button" class="lang-btn" (click)="i18n.toggle()">
      {{ i18n.lang() === 'en' ? 'العربية' : 'English' }}
    </button>
  `,
  styles: `
    .lang-btn {
      border: 1.5px solid var(--wo-border);
      background: var(--wo-surface);
      color: var(--wo-ink);
      border-radius: 999px;
      padding: 6px 14px;
      font: 600 13px var(--wo-font);
      cursor: pointer;
      &:hover { border-color: var(--wo-muted); }
    }
  `,
})
export class LangSwitcher {
  readonly i18n = inject(TranslationService);
}

export interface ConfirmDialogData {
  titleKey: string;
  messageKey: string;
  messageParams?: Record<string, unknown>;
  confirmKey?: string;
  danger?: boolean;
}

/** Small confirmation dialog used for destructive actions. */
@Component({
  selector: 'wo-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ data.titleKey | t }}</h2>
    <mat-dialog-content>{{ data.messageKey | t: data.messageParams }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton (click)="ref.close(false)">{{ 'common.cancel' | t }}</button>
      <button matButton="filled" [class.danger]="data.danger" (click)="ref.close(true)">
        {{ (data.confirmKey ?? 'common.confirm') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .danger { --mdc-filled-button-container-color: var(--wo-danger); }
  `,
})
export class ConfirmDialog {
  readonly ref = inject(MatDialogRef<ConfirmDialog>);
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}

/** Empty-state block with an emoji, message, and optional slot content. */
@Component({
  selector: 'wo-empty-state',
  imports: [TranslatePipe],
  template: `
    <div class="empty">
      <div class="emoji">{{ emoji() }}</div>
      <p class="msg">{{ messageKey() | t }}</p>
      <ng-content />
    </div>
  `,
  styles: `
    .empty { text-align: center; padding: 48px 16px; color: var(--wo-ink-soft); }
    .emoji { font-size: 40px; margin-bottom: 8px; }
    .msg { font-weight: 500; margin-bottom: 12px; }
  `,
})
export class EmptyState {
  readonly emoji = input('🗂️');
  readonly messageKey = input.required<string>();
}

/** Extracts a human-readable message from an API error response. */
export function problemMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ApiProblem | undefined;
    if (problem?.errors) {
      const first = Object.values(problem.errors).flat()[0];
      if (first) return first;
    }
    if (problem?.title) return problem.title;
  }
  return fallback;
}
