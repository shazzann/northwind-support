import type { BadgeTone } from '../../components/Badge';
import type { SlaStatus } from '../../types/api';

export interface SlaPresentation {
  label: string;
  /** A shape as well as a colour, so the state reads without colour vision. */
  symbol: string;
  tone: BadgeTone;
}

/** Every state, so adding one to SlaStatus fails the type check until it is designed here. */
export const slaPresentation: Record<SlaStatus, SlaPresentation> = {
  WithinSla: { label: 'Within SLA', symbol: '●', tone: 'info' },
  AtRisk: { label: 'At risk', symbol: '▲', tone: 'warning' },
  Breached: { label: 'Breached', symbol: '✕', tone: 'danger' },
  Met: { label: 'Met', symbol: '✓', tone: 'success' },
  NotApplicable: { label: 'No SLA', symbol: '–', tone: 'neutral' },
};
