import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LangSwitcher } from '../../shared/components';
import { TranslatePipe } from '../../shared/pipes';

/** Centered card layout shared by all auth pages. */
@Component({
  selector: 'wo-auth-shell',
  imports: [RouterLink, LangSwitcher, TranslatePipe],
  template: `
    <div class="auth">
      <header class="auth__bar container">
        <a routerLink="/" class="auth__logo">Whats<span>Order</span></a>
        <wo-lang-switcher />
      </header>
      <main class="auth__main">
        <div class="auth__card wo-card">
          <h1>{{ titleKey() | t }}</h1>
          <p class="muted">{{ subtitleKey() | t }}</p>
          <ng-content />
        </div>
      </main>
    </div>
  `,
  styles: `
    .auth { min-height: 100dvh; display: flex; flex-direction: column; }
    .auth__bar { display: flex; align-items: center; justify-content: space-between; padding-block: 18px; }
    .auth__logo { font: 800 22px var(--wo-font); color: var(--wo-ink); letter-spacing: -.02em;
      span { color: var(--wo-primary); } }
    .auth__main { flex: 1; display: grid; place-items: start center; padding: 24px 16px 64px; }
    .auth__card { width: 100%; max-width: 420px; padding: 32px 28px; margin-top: 4vh;
      h1 { font-size: 24px; margin-bottom: 4px; }
      p { margin-bottom: 22px; } }
  `,
})
export class AuthShell {
  readonly titleKey = input.required<string>();
  readonly subtitleKey = input.required<string>();
}
