import type { EscalateTicketPayload, TicketDetail, TicketPriority } from '../../types/api';

/** The API's limits (TicketEscalation in the domain), checked here so bad input never leaves the page. */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;

const priorityOrder: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

export type EscalationErrors = Partial<Record<keyof EscalateTicketPayload, string>>;

/** Why the API would refuse to escalate this ticket, or undefined when it would not. */
export function escalationBlocker(ticket: Pick<TicketDetail, 'status' | 'priority'>): string | undefined {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return `${ticket.status} tickets cannot be escalated.`;
  }

  if (ticket.priority === 'Critical') {
    return 'This ticket is already Critical, the highest priority.';
  }

  return undefined;
}

/** The priority an escalation would move the ticket to. */
export function nextPriority(priority: TicketPriority): TicketPriority {
  return priorityOrder[Math.min(priorityOrder.indexOf(priority) + 1, priorityOrder.length - 1)];
}

/** The same rules as the API's validator; an empty object means the payload may be sent. */
export function validateEscalation(payload: EscalateTicketPayload): EscalationErrors {
  const errors: EscalationErrors = {};
  const reasonLength = payload.reason.trim().length;

  if (reasonLength < REASON_MIN_LENGTH || reasonLength > REASON_MAX_LENGTH) {
    errors.reason = `Give a reason of ${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters.`;
  }

  return errors;
}
