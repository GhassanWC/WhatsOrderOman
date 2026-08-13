import { TestBed } from '@angular/core/testing';
import { TranslationService } from '../core/i18n/translation.service';
import { BilingualPipe, OmrPricePipe } from './pipes';

describe('OmrPricePipe', () => {
  let pipe: OmrPricePipe;
  let i18n: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    i18n = TestBed.inject(TranslationService);
    pipe = TestBed.runInInjectionContext(() => new OmrPricePipe());
  });

  it('formats OMR with three decimal places', async () => {
    await i18n.setLang('en');
    expect(pipe.transform(8.5)).toBe('8.500 OMR');
    expect(pipe.transform(14.5)).toBe('14.500 OMR');
    expect(pipe.transform(0)).toBe('0.000 OMR');
  });

  it('uses the rial symbol in Arabic, the default language', () => {
    expect(pipe.transform(8.5)).toBe('8.500 ر.ع.');
  });

  it('returns empty string for null and undefined', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
  });
});

describe('BilingualPipe', () => {
  let pipe: BilingualPipe;
  let i18n: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    i18n = TestBed.inject(TranslationService);
    pipe = TestBed.runInInjectionContext(() => new BilingualPipe());
  });

  it('returns the Arabic value by default, falling back to English', () => {
    expect(pipe.transform('Cakes', 'كيك')).toBe('كيك');
    expect(pipe.transform('Cookies', null)).toBe('Cookies');
  });

  it('returns the English value in English', async () => {
    await i18n.setLang('en');
    expect(pipe.transform('Cakes', 'كيك')).toBe('Cakes');
  });
});
