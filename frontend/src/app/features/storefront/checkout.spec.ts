import { isLikelyValidPhone } from './checkout-page';
import { nextStatusFor } from '../orders/orders-page';

describe('checkout phone validation', () => {
  it('accepts common Omani formats', () => {
    expect(isLikelyValidPhone('91234567')).toBe(true);
    expect(isLikelyValidPhone('71234567')).toBe(true);
    expect(isLikelyValidPhone('9123 4567')).toBe(true);
    expect(isLikelyValidPhone('+968 9123 4567')).toBe(true);
    expect(isLikelyValidPhone('0096891234567')).toBe(true);
  });

  it('accepts international numbers', () => {
    expect(isLikelyValidPhone('+971501234567')).toBe(true);
  });

  it('rejects garbage', () => {
    expect(isLikelyValidPhone('')).toBe(false);
    expect(isLikelyValidPhone('123')).toBe(false);
    expect(isLikelyValidPhone('abcdefgh')).toBe(false);
    expect(isLikelyValidPhone('12345678')).toBe(false); // invalid local prefix
  });
});

describe('order status quick action', () => {
  it('walks the pickup happy path', () => {
    expect(nextStatusFor('New', 'Pickup')).toBe('Confirmed');
    expect(nextStatusFor('Confirmed', 'Pickup')).toBe('Preparing');
    expect(nextStatusFor('Preparing', 'Pickup')).toBe('Ready');
    expect(nextStatusFor('Ready', 'Pickup')).toBe('Completed');
  });

  it('adds the delivery leg for delivery orders', () => {
    expect(nextStatusFor('Ready', 'Delivery')).toBe('OutForDelivery');
    expect(nextStatusFor('OutForDelivery', 'Delivery')).toBe('Completed');
  });

  it('offers nothing for terminal states', () => {
    expect(nextStatusFor('Completed', 'Pickup')).toBeNull();
    expect(nextStatusFor('Cancelled', 'Delivery')).toBeNull();
  });
});
