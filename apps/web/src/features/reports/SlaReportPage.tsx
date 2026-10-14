import { useCallback, useState } from 'react';
import { reportsApi } from '../../api/reports';
import { EmptyState } from '../../components/EmptyState';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useAsyncData } from '../../hooks/useAsyncData';
import type { SlaStatus, SlaSummaryGroup, SlaSummaryGrouping } from '../../types/api';
import { slaPresentation } from '../tickets/sla';
import { criteriaError, initialCriteria, type ReportCriteria, toParams } from './reportCriteria';

const outcomeColumns: { status: SlaStatus; value: (group: SlaSummaryGroup) => number }[] = [
  { status: 'Breached', value: (group) => group.breached },
  { status: 'AtRisk', value: (group) => group.atRisk },
  { status: 'WithinSla', value: (group) => group.withinSla },
  { status: 'Met', value: (group) => group.met },
  { status: 'NotApplicable', value: (group) => group.noSla },
];

/** How each agent, or each priority, is doing against the SLA, for tickets raised in a date range. */
export function SlaReportPage() {
  // What the controls show, and what was last sent: an invalid range never reaches the API.
  const [criteria, setCriteria] = useState<ReportCriteria>(initialCriteria);
  const [applied, setApplied] = useState<ReportCriteria>(initialCriteria);

  const load = useCallback(() => reportsApi.getSlaSummary(toParams(applied)), [applied]);
  const { data, isLoading, error, reload } = useAsyncData(load, 'Unable to load the SLA summary.');

  const rangeError = criteriaError(criteria);

  const update = (patch: Partial<ReportCriteria>) => {
    const next = { ...criteria, ...patch };
    setCriteria(next);

    if (!criteriaError(next)) {
      setApplied(next);
    }
  };

  return (
    <section>
      <header className="page-header">
        <div>
          <h1>SLA summary</h1>
          <p className="page-header__subtitle">
            How tickets raised in a period stand against their SLA, as of now.
          </p>
        </div>
      </header>

      <section className="filters" aria-label="Report options">
        <div className="filters__row">
          <div className="field">
            <label className="field__label" htmlFor="report-group">
              Group by
            </label>
            <select
              id="report-group"
              value={criteria.groupBy}
              onChange={(event) => update({ groupBy: event.target.value as SlaSummaryGrouping })}
            >
              <option value="Agent">Agent</option>
              <option value="Priority">Priority</option>
            </select>
          </div>

          <div className="field">
            <label className="field__label" htmlFor="report-from">
              Raised from
            </label>
            <input
              id="report-from"
              type="date"
              value={criteria.from}
              onChange={(event) => update({ from: event.target.value })}
            />
          </div>

          <div className="field">
            <label className="field__label" htmlFor="report-to">
              Raised to (inclusive)
            </label>
            <input
              id="report-to"
              type="date"
              value={criteria.to}
              aria-invalid={Boolean(rangeError)}
              aria-describedby={rangeError ? 'report-range-error' : undefined}
              onChange={(event) => update({ to: event.target.value })}
            />
          </div>
        </div>

        <div className="filters__row filters__row--actions">
          <p className="field__hint">Dates are UTC days. Leave both empty for every ticket.</p>
          <button
            type="button"
            className="button button--ghost"
            onClick={() => update({ from: '', to: '' })}
          >
            Clear dates
          </button>
        </div>

        {rangeError && (
          <p className="field__error" id="report-range-error" role="alert">
            {rangeError}
          </p>
        )}
      </section>

      {error && <ErrorMessage message={error} onRetry={() => void reload()} />}

      {isLoading && !data && <Spinner label="Loading the SLA summary" />}

      {data && data.groups.length === 0 && (
        <EmptyState title="No tickets were raised in this period" description="Try a wider date range." />
      )}

      {data && data.groups.length > 0 && (
        <div className="table-wrapper" aria-busy={isLoading}>
          <table className="table">
            <caption className="visually-hidden">SLA summary by {data.groupBy.toLowerCase()}</caption>
            <thead>
              <tr>
                <th scope="col">{data.groupBy}</th>
                <th scope="col">Tickets</th>
                {outcomeColumns.map(({ status }) => (
                  <th scope="col" key={status}>
                    <span className="sla-indicator__symbol" aria-hidden="true">
                      {slaPresentation[status].symbol}
                    </span>
                    {slaPresentation[status].label}
                  </th>
                ))}
                <th scope="col">Breach rate</th>
                <th scope="col">Avg. resolution</th>
              </tr>
            </thead>
            <tbody>
              {data.groups.map((group) => (
                <tr key={group.group}>
                  <th scope="row">{group.group}</th>
                  <td>{group.total}</td>
                  {outcomeColumns.map(({ status, value }) => (
                    <td key={status}>{value(group)}</td>
                  ))}
                  <td>{group.breachRate === null ? '–' : `${Math.round(group.breachRate * 100)}%`}</td>
                  <td>{group.averageResolutionHours === null ? '–' : `${group.averageResolutionHours} h`}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
