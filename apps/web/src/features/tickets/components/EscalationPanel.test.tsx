import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail, ticketEscalation } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

async function fillAndSubmit(reason: string, escalatedBy = 'Team Lead') {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Reason'), reason);
  await user.type(screen.getByLabelText('Escalated by'), escalatedBy);
  await user.click(screen.getByRole('button', { name: /escalate to/i }));
}

describe('EscalationPanel', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('blocks a reason that is too short, without calling the API', async () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillAndSubmit('  abc  ');

    expect(screen.getByText('Give a reason of 5 to 500 characters.')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
    expect(escalate).not.toHaveBeenCalled();
  });

  it('shows a 409 from the server and leaves the ticket as it was', async () => {
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated further.'),
    );
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillAndSubmit('Customer cannot pay');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ticket TCK-0012 is already Critical and cannot be escalated further.',
    );
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Escalate to High' })).toBeEnabled();
  });

  it('sends the trimmed reason and hands back the escalated ticket', async () => {
    const result = { ticket: ticketDetail({ priority: 'High' }), escalation: ticketEscalation() };
    escalate.mockResolvedValue(result);
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillAndSubmit('  Customer cannot pay  ');

    expect(escalate).toHaveBeenCalledWith(12, { reason: 'Customer cannot pay', escalatedBy: 'Team Lead' });
    expect(onEscalated).toHaveBeenCalledWith(result);
    expect(screen.getByLabelText('Reason')).toHaveValue('');
  });

  it('shows that it is working while the request is in flight', async () => {
    escalate.mockReturnValue(new Promise(() => {}));
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    await fillAndSubmit('Customer cannot pay');

    expect(screen.getByRole('button', { name: 'Escalating...' })).toBeDisabled();
  });

  it.each([
    [ticketDetail({ priority: 'Critical' }), 'already Critical'],
    [ticketDetail({ status: 'Resolved' }), 'Resolved tickets cannot be escalated.'],
  ])('offers no escalation when the rules would refuse it', (ticket, explanation) => {
    render(<EscalationPanel ticket={ticket} onEscalated={vi.fn()} />);

    expect(screen.queryByRole('button', { name: /escalate/i })).not.toBeInTheDocument();
    expect(screen.getByText(new RegExp(explanation))).toBeInTheDocument();
  });
});
