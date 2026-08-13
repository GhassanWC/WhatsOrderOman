import { Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PublicStoreService } from '../../core/services/public-store.service';
import { CartService } from '../../core/services/cart.service';
import { BilingualPipe, OmrPricePipe, TranslatePipe } from '../../shared/pipes';

@Component({
  selector: 'wo-cart-page',
  imports: [RouterLink, BilingualPipe, OmrPricePipe, TranslatePipe],
  template: `
    <div class="page container">
      <header class="head">
        <a [routerLink]="['/', slug()]" class="back">←</a>
        <h1>{{ 'cart.title' | t }}</h1>
      </header>

      @if (cart.lines().length === 0) {
        <div class="empty">
          <div class="emoji">🛒</div>
          <p class="muted">{{ 'cart.empty' | t }}</p>
          <a class="wo-btn wo-btn--primary" [routerLink]="['/', slug()]">{{ 'cart.browse' | t }}</a>
        </div>
      } @else {
        <div class="lines">
          @for (line of cart.lines(); track line.key) {
            <div class="line wo-card">
              <div class="thumb">
                @if (line.image) {
                  <img [src]="line.image" [alt]="line.name" />
                } @else {
                  <span>{{ line.name.charAt(0) }}</span>
                }
              </div>
              <div class="info">
                <span class="name">{{ line.name | bilingual: line.nameAr }}</span>
                @if (line.optionsLabel) {
                  <span class="opts muted">{{ line.optionsLabel | bilingual: line.optionsLabelAr }}</span>
                }
                <span class="unit">{{ line.unitPrice | omr }}</span>
              </div>
              <div class="controls">
                <div class="stepper">
                  <button type="button" (click)="cart.updateQuantity(line.key, line.quantity - 1)">−</button>
                  <span>{{ line.quantity }}</span>
                  <button type="button" (click)="cart.updateQuantity(line.key, line.quantity + 1)">+</button>
                </div>
                <button type="button" class="remove" (click)="cart.remove(line.key)">{{ 'cart.remove' | t }}</button>
              </div>
            </div>
          }
        </div>

        <div class="summary wo-card">
          <div class="row"><span>{{ 'cart.subtotal' | t }}</span><b>{{ cart.subtotal() | omr }}</b></div>
          @if (cart.savings() > 0) {
            <div class="row save"><span>{{ 'cart.savings' | t }}</span><b>−{{ cart.savings() | omr }}</b></div>
          }
          @if (!cart.meetsMinimum()) {
            <p class="min-warning">⚠️ {{ 'cart.minOrderWarning' | t: { amount: (cart.minimumOrder() | omr) } }}</p>
          }
          <a class="wo-btn wo-btn--primary checkout" [class.disabled]="!cart.meetsMinimum()"
             [routerLink]="cart.meetsMinimum() ? ['/', slug(), 'checkout'] : null">
            {{ 'cart.checkout' | t }} →
          </a>
        </div>
      }
    </div>
  `,
  styles: `
    .page { padding-block: 18px 60px; max-width: 640px; }
    .head { display: flex; align-items: center; gap: 12px; margin-bottom: 18px;
      h1 { font-size: 22px; }
      .back { font-size: 22px; color: var(--wo-ink-soft); } }
    .empty { text-align: center; padding: 60px 0; display: grid; gap: 10px; justify-items: center;
      .emoji { font-size: 46px; } }
    .lines { display: grid; gap: 10px; margin-bottom: 16px; }
    .line { display: flex; gap: 12px; padding: 12px; align-items: center; }
    .thumb { width: 58px; height: 58px; border-radius: 12px; background: var(--wo-primary-soft); overflow: hidden;
      display: grid; place-items: center; font: 800 22px var(--wo-font); color: var(--wo-primary-dark); flex-shrink: 0;
      img { width: 100%; height: 100%; object-fit: cover; } }
    .info { flex: 1; min-width: 0; display: grid; gap: 2px;
      .name { font-weight: 700; }
      .opts { font-size: 12.5px; }
      .unit { font-weight: 600; font-size: 13.5px; color: var(--wo-primary-dark); } }
    .controls { display: grid; gap: 6px; justify-items: end; }
    .stepper { display: flex; align-items: center; gap: 2px; border: 1.5px solid var(--wo-border); border-radius: 999px; padding: 2px;
      button { width: 28px; height: 28px; border: none; border-radius: 50%; background: var(--wo-bg); font-size: 15px; cursor: pointer;
        &:hover { background: var(--wo-primary-soft); } }
      span { min-width: 28px; text-align: center; font-weight: 700; font-size: 14px; } }
    .remove { border: none; background: none; color: var(--wo-danger); font: 600 12px var(--wo-font); cursor: pointer; }
    .summary { padding: 18px; display: grid; gap: 10px;
      .row { display: flex; justify-content: space-between; font-size: 14.5px; }
      .save { color: #15803d; } }
    .min-warning { background: #fefce8; color: #a16207; border-radius: 10px; padding: 10px 12px; font-size: 13.5px; }
    .checkout { width: 100%; &.disabled { opacity: .5; pointer-events: none; } }
  `,
})
export class CartPage {
  readonly slug = input.required<string>();
  readonly cart = inject(CartService);
  private readonly api = inject(PublicStoreService);

  constructor() {
    // Deep links straight to /:slug/cart still need the store context (fees, minimum).
    effect(() => {
      const slug = this.slug();
      if (slug && this.cart.store()?.slug !== slug) {
        this.api.getStore(slug).subscribe((store) => this.cart.openStore(store));
      }
    });
  }
}
