import { TestBed } from '@angular/core/testing';
import { CartService } from './cart.service';
import { PublicProductDto, PublicStoreDto, PublicVariantOptionDto } from '../models/api-types';

function makeStore(overrides: Partial<PublicStoreDto> = {}): PublicStoreDto {
  return {
    id: 's1',
    slug: 'alreem',
    name: 'Al Reem Cakes',
    nameAr: null,
    description: null,
    descriptionAr: null,
    logoUrl: null,
    bannerUrl: null,
    whatsAppNumber: '+96891234567',
    instagramHandle: null,
    locationText: null,
    governorate: null,
    wilayat: null,
    acceptingOrders: true,
    isOpenNow: true,
    openingHours: [],
    deliveryFee: 1.5,
    minimumOrderAmount: 3,
    deliveryEnabled: true,
    pickupEnabled: true,
    currency: 'OMR',
    defaultLanguage: 'en',
    categories: [],
    rating: null,
    reviewsCount: 0,
    offers: [],
    ...overrides,
  };
}

function makeProduct(overrides: Partial<PublicProductDto> = {}): PublicProductDto {
  return {
    id: 'p1',
    categoryId: null,
    name: 'Cookies Box',
    nameAr: null,
    description: null,
    descriptionAr: null,
    price: 3,
    discountedPrice: null,
    inStock: true,
    isFeatured: false,
    images: [],
    variants: [],
    ...overrides,
  };
}

const largeOption: PublicVariantOptionDto = { id: 'opt-large', name: 'Large', nameAr: null, priceAdjustment: 4.5 };

describe('CartService', () => {
  let cart: CartService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    cart = TestBed.inject(CartService);
    cart.openStore(makeStore());
  });

  it('starts empty', () => {
    expect(cart.count()).toBe(0);
    expect(cart.subtotal()).toBe(0);
  });

  it('adds a product and computes the subtotal', () => {
    cart.add(makeProduct(), [], 2);
    expect(cart.count()).toBe(2);
    expect(cart.subtotal()).toBe(6);
  });

  it('merges identical configurations into one line', () => {
    cart.add(makeProduct(), [], 1);
    cart.add(makeProduct(), [], 2);
    expect(cart.lines().length).toBe(1);
    expect(cart.lines()[0].quantity).toBe(3);
  });

  it('keeps different variant selections as separate lines', () => {
    cart.add(makeProduct({ id: 'cake', price: 8.5 }), [], 1);
    cart.add(makeProduct({ id: 'cake', price: 8.5 }), [largeOption], 1);
    expect(cart.lines().length).toBe(2);
  });

  it('applies variant price adjustments', () => {
    cart.add(makeProduct({ id: 'cake', price: 8.5 }), [largeOption], 1);
    expect(cart.subtotal()).toBe(13);
  });

  it('charges the discounted price and tracks savings', () => {
    cart.add(makeProduct({ price: 3, discountedPrice: 2.5 }), [], 2);
    expect(cart.subtotal()).toBe(5);
    expect(cart.savings()).toBe(1);
  });

  it('removes a line when quantity drops to zero', () => {
    cart.add(makeProduct(), [], 1);
    cart.updateQuantity(cart.lines()[0].key, 0);
    expect(cart.count()).toBe(0);
  });

  it('reports the store minimum-order state', () => {
    cart.add(makeProduct({ price: 2 }), [], 1);
    expect(cart.meetsMinimum()).toBe(false);
    cart.add(makeProduct({ id: 'p2', price: 2 }), [], 1);
    expect(cart.meetsMinimum()).toBe(true);
  });

  it('restores a persisted cart for the same store', () => {
    localStorage.setItem('wo.cart.dates', JSON.stringify({
      lines: [{
        key: 'x', productId: 'x', name: 'Dates', nameAr: null, image: null,
        unitPrice: 2, originalUnitPrice: 2, quantity: 3, optionIds: [],
        optionsLabel: null, optionsLabelAr: null,
      }],
    }));
    cart.openStore(makeStore({ slug: 'dates' }));
    expect(cart.count()).toBe(3);
    expect(cart.subtotal()).toBe(6);
  });

  it('switching stores swaps to that store cart', () => {
    cart.add(makeProduct(), [], 2);
    cart.openStore(makeStore({ slug: 'other' }));
    expect(cart.count()).toBe(0);
  });
});
