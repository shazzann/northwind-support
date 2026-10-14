import type { SlaSummaryParams } from '../../api/reports';
import type { SlaSummaryGrouping } from '../../types/api';

/** The report's controls as the user set them: dates are whole days (YYYY-MM-DD), or empty. */
export interface ReportCriteria {
  groupBy: SlaSummaryGrouping;
  from: string;
  to: string;
}

export const initialCriteria: ReportCriteria = { groupBy: 'Agent', from: '', to: '' };

/** Why these criteria cannot be sent, or undefined when they can. */
export function criteriaError({ from, to }: ReportCriteria): string | undefined {
  return from && to && to < from ? 'The end date is before the start date.' : undefined;
}

/**
 * The API's range is [from, to) in UTC. A person picking "to 14 October" means the whole of
 * that day, so the request ends at the start of the next one.
 */
export function toParams({ groupBy, from, to }: ReportCriteria): SlaSummaryParams {
  return {
    groupBy,
    fromUtc: from ? `${from}T00:00:00Z` : undefined,
    toUtc: to ? nextDay(to) : undefined,
  };
}

function nextDay(day: string): string {
  const date = new Date(`${day}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().replace('.000Z', 'Z');
}
