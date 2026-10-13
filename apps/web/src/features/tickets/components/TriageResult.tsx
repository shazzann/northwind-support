import { formatDateTime } from '../../../lib/format';
import type { RaisedTicket } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';
import { PriorityBadge } from './TicketBadges';

interface TriageResultProps {
  raised: RaisedTicket;
}

/** What triage decided for a new ticket - priority, due date, owner - and why. */
export function TriageResult({ raised }: TriageResultProps) {
  const { ticket, triage } = raised;

  return (
    <dl className="summary triage">
      <div>
        <dt>Priority</dt>
        <dd>
          <PriorityBadge priority={ticket.priority} />
          <span className="summary__note">{triage.priorityReason}</span>
        </dd>
      </div>

      <div>
        <dt>Due</dt>
        <dd>
          {formatDateTime(ticket.dueAtUtc)}
          <SlaIndicator slaStatus={ticket.slaStatus} />
          <span className="summary__note">{triage.slaReason}</span>
        </dd>
      </div>

      <div>
        <dt>Assigned agent</dt>
        <dd>
          {ticket.assignedAgent?.fullName ?? 'Unassigned'}
          <span className="summary__note">{triage.assignmentReason}</span>
        </dd>
      </div>
    </dl>
  );
}
