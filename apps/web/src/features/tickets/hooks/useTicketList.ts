import { useCallback, useEffect, useMemo, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketFilters } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  // The filters the request is built from. Being state, its identity changes only when one of
  // them changes, so it is the one dependency of the fetch: a new filter needs no extra wiring.
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);

  // What is in the search box right now. It reaches `filters` once the typing settles, so a
  // burst of keystrokes sends one request rather than one each.
  const [searchText, setSearchText] = useState(emptyFilters.search);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setFilters((current) =>
        current.search === searchText ? current : { ...current, search: searchText, page: 1 },
      );
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [searchText]);

  const load = useCallback(() => ticketsApi.getTickets(filters), [filters]);
  const list = useAsyncData(load, 'Unable to load tickets.');

  const updateFilters = useCallback((patch: Partial<TicketFilters>) => {
    const { search, ...rest } = patch;

    if (search !== undefined) {
      setSearchText(search);
    }

    if (Object.keys(rest).length > 0) {
      setFilters((current) => ({ ...current, ...rest, page: rest.page ?? 1 }));
    }
  }, []);

  const resetFilters = useCallback(() => {
    setSearchText(emptyFilters.search);
    setFilters(emptyFilters);
  }, []);

  // The filter bar shows the search box as typed, not as last sent.
  const visibleFilters = useMemo(() => ({ ...filters, search: searchText }), [filters, searchText]);

  return {
    filters: visibleFilters,
    updateFilters,
    resetFilters,
    reload: list.reload,
    isLoading: list.isLoading,
    data: list.data,
    error: list.error,
  };
}
