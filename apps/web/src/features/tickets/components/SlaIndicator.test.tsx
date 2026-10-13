import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

const states: [SlaStatus, string][] = [
  ['WithinSla', 'Within SLA'],
  ['AtRisk', 'At risk'],
  ['Breached', 'Breached'],
  ['Met', 'Met'],
  ['NotApplicable', 'No SLA'],
];

describe('SlaIndicator', () => {
  it.each(states)('names the %s state in words', (status, label) => {
    render(<SlaIndicator slaStatus={status} />);

    expect(screen.getByText(label)).toBeVisible();
  });

  it('tells every state apart by text and by shape, not by colour alone', () => {
    const rendered = states.map(([status]) => {
      const { container, unmount } = render(<SlaIndicator slaStatus={status} />);
      const badge = container.querySelector('.badge');
      const result = {
        text: badge?.textContent,
        symbol: container.querySelector('[aria-hidden="true"]')?.textContent,
        tone: badge?.className,
      };
      unmount();
      return result;
    });

    expect(new Set(rendered.map((state) => state.text)).size).toBe(states.length);
    expect(new Set(rendered.map((state) => state.symbol)).size).toBe(states.length);
    expect(new Set(rendered.map((state) => state.tone)).size).toBe(states.length);
  });

  it('shows the due date when given one, and says when there is none', () => {
    const { rerender } = render(<SlaIndicator slaStatus="WithinSla" dueAtUtc="2026-09-24T16:12:00Z" />);
    expect(screen.getByText(/^Due /)).toBeInTheDocument();

    rerender(<SlaIndicator slaStatus="NotApplicable" dueAtUtc={null} />);
    expect(screen.getByText('No due date')).toBeInTheDocument();
  });
});
