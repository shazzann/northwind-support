import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

/** Lets pending promises and effects settle without moving the clock forward. */
const flush = () =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  // DEFECT-117: only search/page/pageSize used to trigger a request.
  it.each<[string, Partial<TicketFilters>]>([
    ['status', { status: 'InProgress' }],
    ['priority', { priority: 'High' }],
    ['category', { categoryId: 2 }],
    ['customer', { customerId: 3 }],
    ['assigned agent', { assignedAgentId: 4 }],
    ['unassigned only', { unassignedOnly: true }],
    ['sort', { sortBy: 'dueAtUtc', sortDirection: 'asc' }],
    ['SLA status', { slaStatus: 'AtRisk' }],
  ])('sends a request with the new %s as soon as it changes', async (_, patch) => {
    const { result } = renderHook(() => useTicketList());
    await flush();
    getTickets.mockClear();

    act(() => {
      result.current.updateFilters(patch);
    });
    await flush();

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ ...patch, page: 1 }));
  });

  it('keeps sending filter changes after a search is typed and cleared', async () => {
    const { result } = renderHook(() => useTicketList());
    await flush();

    act(() => {
      result.current.updateFilters({ search: 'outage' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    act(() => {
      result.current.updateFilters({ search: '' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    getTickets.mockClear();

    act(() => {
      result.current.updateFilters({ status: 'Open' });
    });
    await flush();

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'Open', search: '' }),
    );
  });

  it('sends one request for a burst of keystrokes, not one per keystroke', async () => {
    const { result } = renderHook(() => useTicketList());
    await flush();
    getTickets.mockClear();

    for (const text of ['i', 'in', 'inv', 'invo']) {
      act(() => {
        result.current.updateFilters({ search: text });
      });
      await act(async () => {
        await vi.advanceTimersByTimeAsync(100);
      });
    }

    expect(getTickets).not.toHaveBeenCalled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'invo' }));
  });

  it('shows what was typed straight away, before the request goes out', () => {
    const { result } = renderHook(() => useTicketList());

    act(() => {
      result.current.updateFilters({ search: 'inv' });
    });

    expect(result.current.filters.search).toBe('inv');
  });
});
