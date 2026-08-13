import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LangSwitcher } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';

@Component({
  selector: 'wo-landing-page',
  imports: [RouterLink, LangSwitcher, TranslatePipe],
  template: `
    <div class="landing">
      <header class="bar container">
        <span class="logo">Whats<span class="accent">Order</span></span>
        <span class="spacer"></span>
        <wo-lang-switcher />
        @if (auth.isAuthenticated()) {
          <a class="wo-btn wo-btn--primary mini" routerLink="/dashboard">{{ 'nav.home' | t }}</a>
        } @else {
          <a class="signin" routerLink="/login">{{ 'landing.signIn' | t }}</a>
        }
      </header>

      <section class="hero container">
        <p class="tag">🇴🇲 {{ 'landing.tagline' | t }}</p>
        <h1>{{ 'landing.heroTitle' | t }}</h1>
        <p class="sub">{{ 'landing.heroText' | t }}</p>
        <div class="cta-row">
          <a class="wo-btn wo-btn--primary big" [routerLink]="ctaLink()" [queryParams]="ctaParams()">
            {{ 'landing.getStarted' | t }}
          </a>
          <a class="wo-btn wo-btn--ghost big" routerLink="/alreem">{{ 'landing.seeDemo' | t }} →</a>
        </div>

        <div class="mock">
          <div class="mock__bubble mock__bubble--in">مرحباً! أريد كيكة الشوكولاتة 🎂</div>
          <div class="mock__bubble mock__bubble--out">
            Your order <b>#WO-1024</b> has been received ✅<br />
            <span class="mock__total">Total: 14.500 OMR</span>
          </div>
        </div>
      </section>

      <section class="features container">
        <div class="feature wo-card">
          <div class="ficon">🔗</div>
          <h3>{{ 'landing.feature1Title' | t }}</h3>
          <p class="muted">{{ 'landing.feature1Text' | t }}</p>
        </div>
        <div class="feature wo-card">
          <div class="ficon">🧾</div>
          <h3>{{ 'landing.feature2Title' | t }}</h3>
          <p class="muted">{{ 'landing.feature2Text' | t }}</p>
        </div>
        <div class="feature wo-card">
          <div class="ficon">💬</div>
          <h3>{{ 'landing.feature3Title' | t }}</h3>
          <p class="muted">{{ 'landing.feature3Text' | t }}</p>
        </div>
      </section>

      <section class="pricing container">
        <h2>{{ 'landing.pricingTitle' | t }}</h2>
        <div class="plans">
          <div class="plan wo-card">
            <h3>{{ 'landing.free' | t }}</h3>
            <div class="price">{{ 'landing.freePrice' | t }}</div>
            <ul>
              <li>✓ {{ 'landing.freeFeature1' | t }}</li>
              <li>✓ {{ 'landing.freeFeature2' | t }}</li>
              <li>✓ {{ 'landing.freeFeature3' | t }}</li>
            </ul>
            <a class="wo-btn wo-btn--ghost" [routerLink]="ctaLink()" [queryParams]="ctaParams()">
              {{ 'landing.getStarted' | t }}
            </a>
          </div>
          <div class="plan plan--pro wo-card">
            <h3>{{ 'landing.pro' | t }}</h3>
            <div class="price">{{ 'landing.proPrice' | t }}</div>
            <ul>
              <li>✓ {{ 'landing.proFeature1' | t }}</li>
              <li>✓ {{ 'landing.proFeature2' | t }}</li>
              <li>✓ {{ 'landing.proFeature3' | t }}</li>
            </ul>
            <a class="wo-btn wo-btn--primary" [routerLink]="ctaLink()" [queryParams]="ctaParams()">
              {{ 'landing.getStarted' | t }}
            </a>
          </div>
        </div>
      </section>

      <footer class="foot muted">{{ 'landing.footer' | t }}</footer>
    </div>
  `,
  styles: `
    .landing { min-height: 100dvh; }
    .bar { display: flex; align-items: center; gap: 14px; padding-block: 18px; }
    .logo { font: 800 22px var(--wo-font); .accent { color: var(--wo-primary); } }
    .signin { font-weight: 600; }
    .mini { padding: 8px 18px; font-size: 13.5px; }
    .hero { text-align: center; padding: 44px 20px 24px; max-width: 780px;
      .tag { display: inline-block; background: var(--wo-primary-soft); color: var(--wo-primary-dark);
        border-radius: 999px; padding: 6px 16px; font-weight: 600; font-size: 13.5px; margin-bottom: 18px; }
      h1 { font-size: clamp(30px, 6vw, 46px); line-height: 1.15; letter-spacing: -.02em; }
      .sub { font-size: 17px; color: var(--wo-ink-soft); margin: 16px auto 26px; max-width: 560px; } }
    .cta-row { display: flex; gap: 12px; justify-content: center; flex-wrap: wrap; }
    .big { padding: 14px 28px; font-size: 15.5px; }
    .mock { margin: 44px auto 0; max-width: 420px; display: grid; gap: 10px; }
    .mock__bubble { border-radius: 16px; padding: 12px 16px; font-size: 14.5px; box-shadow: var(--wo-shadow);
      max-width: 85%; text-align: start; }
    .mock__bubble--in { background: var(--wo-surface); justify-self: start; border-start-start-radius: 4px; }
    .mock__bubble--out { background: #dcfce7; justify-self: end; border-start-end-radius: 4px;
      .mock__total { font-weight: 700; } }
    .features { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; padding-block: 44px; }
    .feature { padding: 24px; display: grid; gap: 8px;
      .ficon { font-size: 30px; }
      h3 { font-size: 17px; }
      p { font-size: 14px; } }
    .pricing { text-align: center; padding-bottom: 40px;
      h2 { font-size: 26px; margin-bottom: 22px; } }
    .plans { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 300px)); gap: 16px; justify-content: center; }
    .plan { padding: 26px; display: grid; gap: 12px; justify-items: center;
      h3 { font-size: 18px; }
      .price { font: 800 26px var(--wo-font); color: var(--wo-primary-dark); }
      ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; color: var(--wo-ink-soft); font-size: 14px; } }
    .plan--pro { border: 2px solid var(--wo-primary); }
    .foot { text-align: center; padding: 28px 0 36px; font-size: 13.5px; }
  `,
})
export class LandingPage {
  readonly auth = inject(AuthService);

  // "Create your store" continues the seller journey: signed-in users go to the
  // dashboard (the storeGuard detours through onboarding when there is no store
  // yet); guests register as sellers first.
  readonly ctaLink = computed(() => (this.auth.isAuthenticated() ? '/dashboard' : '/register'));
  readonly ctaParams = computed(() =>
    this.auth.isAuthenticated() ? null : { as: 'seller' },
  );
}
