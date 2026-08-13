import { OrderStatusHistoryDto } from '../../core/models/api-types';
import { buildTimeline } from './order-confirmation-page';

function history(...entries: [OrderStatusHistoryDto['newStatus'], string][]): OrderStatusHistoryDto[] {
  return entries.map(([newStatus, changedAt], i) => ({
    previousStatus: i === 0 ? null : entries[i - 1][0],
    newStatus,
    changedBy: i === 0 ? 'customer' : 'store',
    changedAt,
  }));
}

describe('buildTimeline', () => {
  it('marks reached steps done and the current step for a pickup order', () => {
    const timeline = buildTimeline({
      status: 'Preparing',
      fulfillmentMethod: 'Pickup',
      statusHistory: history(['New', 't1'], ['Confirmed', 't2'], ['Preparing', 't3']),
    });

    expect(timeline.map((s) => s.status)).toEqual(['New', 'Confirmed', 'Preparing', 'Ready', 'Completed']);
    expect(timeline.filter((s) => s.done).map((s) => s.status)).toEqual(['New', 'Confirmed', 'Preparing']);
    expect(timeline.find((s) => s.current)?.status).toBe('Preparing');
    expect(timeline[1].at).toBe('t2');
    expect(timeline[3].at).toBeNull();
  });

  it('includes OutForDelivery only for delivery orders', () => {
    const delivery = buildTimeline({
      status: 'New', fulfillmentMethod: 'Delivery', statusHistory: history(['New', 't1']),
    });
    expect(delivery.map((s) => s.status)).toContain('OutForDelivery');

    const pickup = buildTimeline({
      status: 'New', fulfillmentMethod: 'Pickup', statusHistory: history(['New', 't1']),
    });
    expect(pickup.map((s) => s.status)).not.toContain('OutForDelivery');
  });

  it('renders a rejected order as reached steps plus a terminal entry', () => {
    const timeline = buildTimeline({
      status: 'Rejected',
      fulfillmentMethod: 'Pickup',
      statusHistory: history(['New', 't1'], ['Rejected', 't2']),
    });

    expect(timeline.map((s) => s.status)).toEqual(['New', 'Rejected']);
    expect(timeline.every((s) => s.done)).toBe(true);
    expect(timeline.at(-1)).toMatchObject({ status: 'Rejected', current: true, at: 't2' });
  });

  it('renders a cancellation mid-flow after the reached steps', () => {
    const timeline = buildTimeline({
      status: 'Cancelled',
      fulfillmentMethod: 'Delivery',
      statusHistory: history(['New', 't1'], ['Confirmed', 't2'], ['Cancelled', 't3']),
    });

    expect(timeline.map((s) => s.status)).toEqual(['New', 'Confirmed', 'Cancelled']);
    expect(timeline.at(-1)?.current).toBe(true);
  });
});
