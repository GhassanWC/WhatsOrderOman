import { Injectable, computed, signal } from '@angular/core';

export type Lang = 'en' | 'ar';

/**
 * Lightweight runtime i18n: JSON dictionaries per language, signal-driven so every
 * `t()` call in a template re-evaluates when the language changes. Adding a language
 * later = dropping a new JSON file into /i18n and extending the Lang type.
 */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private static readonly STORAGE_KEY = 'wo.lang';

  readonly lang = signal<Lang>(this.restore());
  readonly dir = computed(() => (this.lang() === 'ar' ? 'rtl' : 'ltr'));

  private readonly dict = signal<Record<string, string>>({});
  private readonly cache = new Map<Lang, Record<string, string>>();

  /** Called by the app initializer so the first render already has translations. */
  async init(): Promise<void> {
    this.applyDocumentAttributes();
    await this.load(this.lang());
  }

  async setLang(lang: Lang): Promise<void> {
    if (lang === this.lang()) return;
    localStorage.setItem(TranslationService.STORAGE_KEY, lang);
    this.lang.set(lang);
    this.applyDocumentAttributes();
    await this.load(lang);
  }

  async toggle(): Promise<void> {
    await this.setLang(this.lang() === 'en' ? 'ar' : 'en');
  }

  /** Translate a key with optional {{param}} interpolation. Unknown keys return the key. */
  readonly t = (key: string, params?: Record<string, unknown>): string => {
    let value = this.dict()[key] ?? key;
    if (params) {
      for (const [name, replacement] of Object.entries(params)) {
        value = value.replaceAll(`{{${name}}}`, String(replacement));
      }
    }
    return value;
  };

  /** Picks the Arabic variant of a bilingual field when the UI is in Arabic. */
  readonly pick = (en: string | null | undefined, ar: string | null | undefined): string =>
    (this.lang() === 'ar' && ar ? ar : (en ?? ar ?? ''));

  private async load(lang: Lang): Promise<void> {
    const cached = this.cache.get(lang);
    if (cached) {
      this.dict.set(cached);
      return;
    }
    try {
      const response = await fetch(`/i18n/${lang}.json`);
      const nested = (await response.json()) as Record<string, unknown>;
      const flat = flatten(nested);
      this.cache.set(lang, flat);
      this.dict.set(flat);
    } catch {
      // Keys render as-is when the dictionary cannot be loaded.
      this.dict.set({});
    }
  }

  /** Arabic is the default; English only when the visitor has explicitly picked it. */
  private restore(): Lang {
    const stored = typeof localStorage !== 'undefined'
      ? localStorage.getItem(TranslationService.STORAGE_KEY)
      : null;
    return stored === 'en' ? 'en' : 'ar';
  }

  private applyDocumentAttributes(): void {
    document.documentElement.lang = this.lang();
    document.documentElement.dir = this.dir();
  }
}

function flatten(nested: Record<string, unknown>, prefix = ''): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(nested)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object') {
      Object.assign(result, flatten(value as Record<string, unknown>, path));
    } else {
      result[path] = String(value);
    }
  }
  return result;
}
