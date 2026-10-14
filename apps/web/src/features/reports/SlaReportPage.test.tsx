import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { reportsApi } from '../../api/reports';
import type { SlaSummary, SlaSummaryGroup } from '../../types/api';
import { SlaReportPage } from './SlaReportPage';

vi.mock('../../api/reports', () => ({ reportsApi: { getSlaSummary: vi.fn() } }));

const getSlaSummary = vi.mocked(reportsApi.getSlaSummary);

const group = (overrides: Partial<SlaSummaryGroup>): SlaSummaryGroup => ({
  group: 'Priya Nair',
  total: 7,
  breached: 4,
  atRisk: 1,
  withinSla: 1,
  met: 1,
  noSla: 0,
  breachRate: 0.571,
  averageResolutionHours: 7.7,
  ...overrides,
});

const summary = (groups: SlaSummaryGroup[]): SlaSummary => ({ from: null, to: null, groupBy: 'Agent', groups });

describe('SlaReportPage', () => {
  beforeEach(() => {
    getSlaSummary.mockResolvedValue(
      summary([group({}), group({ group: 'Unassigned', total: 2, breached: 0, met: 0, breachRate: null, averageResolutionHours: null })]),
    );
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('shows each group with its rate as a percentage, and a dash where there is nothing to show', async () => {
    render(<SlaReportPage />);

    const priya = (await screen.findByRole('rowheader', { name: 'Priya Nair' })).closest('tr')!;
    expect(within(priya).getByText('57%')).toBeInTheDocument();
    expect(within(priya).getByText('7.7 h')).toBeInTheDocument();

    const unassigned = screen.getByRole('rowheader', { name: 'Unassigned' }).closest('tr')!;
    expect(within(unassigned).getAllByText('–')).toHaveLength(2);
    expect(getSlaSummary).toHaveBeenCalledWith({ groupBy: 'Agent', fromUtc: undefined, toUtc: undefined });
  });

  it('asks again, grouped by priority, as soon as the grouping changes', async () => {
    const user = userEvent.setup();
    render(<SlaReportPage />);
    await screen.findByRole('rowheader', { name: 'Priya Nair' });

    await user.selectOptions(screen.getByLabelText('Group by'), 'Priority');

    expect(getSlaSummary).toHaveBeenLastCalledWith(expect.objectContaining({ groupBy: 'Priority' }));
  });

  it('sends whole days, with the end date included', async () => {
    render(<SlaReportPage />);
    await screen.findByRole('rowheader', { name: 'Priya Nair' });

    fireEvent.change(screen.getByLabelText('Raised from'), { target: { value: '2026-10-01' } });
    fireEvent.change(screen.getByLabelText('Raised to (inclusive)'), { target: { value: '2026-10-14' } });

    expect(getSlaSummary).toHaveBeenLastCalledWith({
      groupBy: 'Agent',
      fromUtc: '2026-10-01T00:00:00Z',
      toUtc: '2026-10-15T00:00:00Z',
    });
  });

  it('refuses a range that ends before it starts, without asking the API', async () => {
    render(<SlaReportPage />);
    await screen.findByRole('rowheader', { name: 'Priya Nair' });
    fireEvent.change(screen.getByLabelText('Raised from'), { target: { value: '2026-10-14' } });
    getSlaSummary.mockClear();

    fireEvent.change(screen.getByLabelText('Raised to (inclusive)'), { target: { value: '2026-10-11' } });

    expect(screen.getByRole('alert')).toHaveTextContent('The end date is before the start date.');
    expect(screen.getByLabelText('Raised to (inclusive)')).toHaveAttribute('aria-invalid', 'true');
    expect(getSlaSummary).not.toHaveBeenCalled();
  });

  it('says so when no tickets were raised in the period', async () => {
    getSlaSummary.mockResolvedValue(summary([]));

    render(<SlaReportPage />);

    expect(await screen.findByText('No tickets were raised in this period')).toBeInTheDocument();
  });
});
