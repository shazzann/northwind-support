import { Badge } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaPresentation } from '../sla';

interface SlaIndicatorProps {
  slaStatus: SlaStatus;
  /** Shown under the status when given. */
  dueAtUtc?: string | null;
}

/** Where a ticket stands against its SLA. The one SLA display, on the list and the detail page. */
export function SlaIndicator({ slaStatus, dueAtUtc }: SlaIndicatorProps) {
  const { label, symbol, tone } = slaPresentation[slaStatus];

  return (
    <div className="sla-indicator">
      <Badge tone={tone}>
        <span className="sla-indicator__symbol" aria-hidden="true">
          {symbol}
        </span>
        <span>{label}</span>
      </Badge>
      {dueAtUtc !== undefined && (
        <span className="sla-indicator__due">
          {dueAtUtc ? `Due ${formatDateTime(dueAtUtc)}` : 'No due date'}
        </span>
      )}
    </div>
  );
}
