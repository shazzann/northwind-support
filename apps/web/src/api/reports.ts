import type { SlaSummary, SlaSummaryGrouping } from '../types/api';
import { request } from './client';

/** What the SLA summary covers: tickets created in [fromUtc, toUtc), grouped one way. */
export interface SlaSummaryParams {
  groupBy: SlaSummaryGrouping;
  fromUtc?: string;
  toUtc?: string;
}

export const reportsApi = {
  getSlaSummary: ({ groupBy, fromUtc, toUtc }: SlaSummaryParams) => {
    const params = new URLSearchParams({ groupBy });

    if (fromUtc) {
      params.set('from', fromUtc);
    }

    if (toUtc) {
      params.set('to', toUtc);
    }

    return request<SlaSummary>(`/reports/sla-summary?${params.toString()}`);
  },
};
