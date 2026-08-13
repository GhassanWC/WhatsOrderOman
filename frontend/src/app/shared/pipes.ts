import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from '../core/i18n/translation.service';

/** `{{ 'nav.orders' | t }}` — impure so language switches re-render instantly. */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(key: string, params?: Record<string, unknown>): string {
    return this.i18n.t(key, params);
  }
}

/** Formats OMR with its 3 decimal places: `8.500 OMR` / `8.500 ر.ع.` */
@Pipe({ name: 'omr', pure: false })
export class OmrPricePipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '';
    const amount = value.toFixed(3);
    return this.i18n.lang() === 'ar' ? `${amount} ر.ع.` : `${amount} OMR`;
  }
}

/** Picks the Arabic variant of a bilingual value when the UI is in Arabic. */
@Pipe({ name: 'bilingual', pure: false })
export class BilingualPipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(en: string | null | undefined, ar: string | null | undefined): string {
    return this.i18n.pick(en, ar);
  }
}
