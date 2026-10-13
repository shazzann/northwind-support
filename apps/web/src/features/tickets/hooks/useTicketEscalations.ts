import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';

/** A ticket's escalation history, newest first; refetches whenever the id changes. */
export function useTicketEscalations(ticketId: number) {
  const load = useCallback(() => ticketsApi.getEscalations(ticketId), [ticketId]);

  return useAsyncData(load, 'Unable to load the escalation history.');
}
