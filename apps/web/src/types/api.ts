/**
 * Types for everything the API sends and receives. Keep these in step with the DTOs in
 * src/Application - they are the contract between the two halves of the application.
 */

export type CustomerTier = 'Standard' | 'Premium';

export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export type TicketStatus = 'New' | 'Open' | 'InProgress' | 'Resolved' | 'Closed';

export type SlaStatus = 'NotApplicable' | 'WithinSla' | 'AtRisk' | 'Breached' | 'Met';

export type TicketSortField = 'createdAtUtc' | 'updatedAtUtc' | 'dueAtUtc' | 'priority' | 'status';

export type SortDirection = 'asc' | 'desc';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CustomerSummary {
  id: number;
  name: string;
  tier: CustomerTier;
}

export interface CustomerContact extends CustomerSummary {
  email: string;
  phone: string | null;
}

export interface CategorySummary {
  id: number;
  name: string;
}

export interface CategoryOnTicket extends CategorySummary {
  requiresSpecialist: boolean;
}

export interface AgentSummary {
  id: number;
  fullName: string;
}

export interface TicketListItem {
  id: number;
  reference: string;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  customer: CustomerSummary;
  category: CategorySummary;
  assignedAgent: AgentSummary | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  dueAtUtc: string | null;
  resolvedAtUtc: string | null;
  slaStatus: SlaStatus;
}

export interface TicketDetail {
  id: number;
  reference: string;
  title: string;
  description: string;
  status: TicketStatus;
  priority: TicketPriority;
  customer: CustomerContact;
  category: CategoryOnTicket;
  assignedAgent: AgentSummary | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  dueAtUtc: string | null;
  resolvedAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** Everything the ticket list screen can narrow the results by. */
export interface TicketFilters {
  search: string;
  status?: TicketStatus;
  priority?: TicketPriority;
  categoryId?: number;
  customerId?: number;
  assignedAgentId?: number;
  unassignedOnly?: boolean;
  slaStatus?: SlaStatus;
  page: number;
  pageSize: number;
  sortBy: TicketSortField;
  sortDirection: SortDirection;
}

/** Why triage gave a new ticket its priority, due date and owner. */
export interface TicketTriage {
  priorityReason: string;
  slaWindowMinutes: number;
  slaReason: string;
  assignmentReason: string;
}

/** Response of POST /api/tickets: the ticket as triaged, and the reasons. */
export interface RaisedTicket {
  ticket: TicketDetail;
  triage: TicketTriage;
}

/** Body of POST /api/tickets/{id}/escalate. Who escalates comes from the sign-in token. */
export interface EscalateTicketPayload {
  reason: string;
}

/** One recorded escalation, with the before and after of everything it changed. */
export interface TicketEscalation {
  id: number;
  fromPriority: TicketPriority;
  toPriority: TicketPriority;
  fromAgent: AgentSummary | null;
  toAgent: AgentSummary | null;
  fromDueAtUtc: string | null;
  toDueAtUtc: string;
  reason: string;
  escalatedBy: string;
  escalatedAtUtc: string;
}

/** Response of POST /api/tickets/{id}/escalate. */
export interface EscalationResult {
  ticket: TicketDetail;
  escalation: TicketEscalation;
}

export interface CreateTicketPayload {
  title: string;
  description: string;
  customerId: number;
  categoryId: number;
  requestedPriority?: TicketPriority;
}

export interface Agent {
  id: number;
  fullName: string;
  email: string;
  isActive: boolean;
  maxOpenTickets: number;
  openTicketCount: number;
  specializations: CategorySummary[];
}

export interface Category {
  id: number;
  name: string;
  isActive: boolean;
  requiresSpecialist: boolean;
  forcesCriticalPriority: boolean;
}

export interface CustomerListItem {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  tier: CustomerTier;
  openTicketCount: number;
}

export interface CustomerDetail {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  tier: CustomerTier;
  createdAtUtc: string;
  tickets: TicketListItem[];
}

export type SlaSummaryGrouping = 'Agent' | 'Priority';

/** One row of the SLA summary: an agent ("Unassigned" included) or a priority. */
export interface SlaSummaryGroup {
  group: string;
  total: number;
  breached: number;
  atRisk: number;
  withinSla: number;
  met: number;
  noSla: number;
  /** Breached as a share (0-1) of tickets with an SLA; null when none have one. */
  breachRate: number | null;
  /** Creation to resolution, over resolved tickets; null when none are resolved. */
  averageResolutionHours: number | null;
}

/** Response of GET /api/reports/sla-summary. */
export interface SlaSummary {
  from: string | null;
  to: string | null;
  groupBy: SlaSummaryGrouping;
  groups: SlaSummaryGroup[];
}

/** The signed-in agent, enough to greet them. */
export interface SignedInAgent {
  id: number;
  fullName: string;
  email: string;
}

/** Response of POST /api/auth/login. */
export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  agent: SignedInAgent;
}

/** RFC 7807 problem document, which is how the API reports every failure. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
