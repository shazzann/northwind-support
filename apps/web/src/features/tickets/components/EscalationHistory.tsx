import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  escalations?: TicketEscalation[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

/** Every escalation of a ticket, newest first: what changed, why, who and when. */
export function EscalationHistory({ escalations, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <section className="card" aria-labelledby="escalation-history-heading">
      <h2 id="escalation-history-heading">Escalation history</h2>

      {isLoading && !escalations && <Spinner label="Loading escalation history" />}

      {error && <ErrorMessage message={error} onRetry={onRetry} />}

      {escalations && escalations.length === 0 && <p className="field__hint">Not escalated yet.</p>}

      {escalations && escalations.length > 0 && (
        <ol className="history">
          {escalations.map((escalation) => (
            <li key={escalation.id} className="history__entry">
              <p className="history__headline">
                <strong>
                  {escalation.fromPriority} → {escalation.toPriority}
                </strong>{' '}
                by {escalation.escalatedBy}, {formatDateTime(escalation.escalatedAtUtc)}
              </p>

              <p className="history__reason">{escalation.reason}</p>

              <dl className="history__changes">
                <dt>Agent</dt>
                <dd>
                  {agentName(escalation.fromAgent)} → {agentName(escalation.toAgent)}
                </dd>
                <dt>Due</dt>
                <dd>
                  {formatDateTime(escalation.fromDueAtUtc)} → {formatDateTime(escalation.toDueAtUtc)}
                </dd>
              </dl>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function agentName(agent: AgentSummary | null): string {
  return agent?.fullName ?? 'Unassigned';
}
