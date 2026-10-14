import type { Session } from '../auth/session';
import type { PagedResult, TicketDetail, TicketEscalation, TicketListItem } from '../types/api';

/** A list row with sensible defaults, so tests only state what they care about. */
export function ticketListItem(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard' },
    category: { id: 2, name: 'Billing' },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

export function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
  };
}

/** A ticket as the detail page sees it, with sensible defaults. */
export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    description: 'Invoice 5512 is EUR 240 higher than the confirmed order.',
    status: 'InProgress',
    priority: 'Medium',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard', email: 'help@fabrikam.example', phone: null },
    category: { id: 2, name: 'Billing', requiresSpecialist: true },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-25T08:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'WithinSla',
    ...overrides,
  };
}

export function ticketEscalation(overrides: Partial<TicketEscalation> = {}): TicketEscalation {
  return {
    id: 1,
    fromPriority: 'Medium',
    toPriority: 'High',
    fromAgent: { id: 4, fullName: 'Sara Lindqvist' },
    toAgent: { id: 4, fullName: 'Sara Lindqvist' },
    fromDueAtUtc: '2026-09-25T08:12:00Z',
    toDueAtUtc: '2026-09-24T18:00:00Z',
    reason: 'Customer cannot pay their suppliers',
    escalatedBy: 'Team Lead',
    escalatedAtUtc: '2026-09-24T10:00:00Z',
    ...overrides,
  };
}

/** A signed-in session that is good for another hour. */
export function session(overrides: Partial<Session> = {}): Session {
  return {
    token: 'header.payload.signature',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    agent: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
    ...overrides,
  };
}
