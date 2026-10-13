import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { agentsApi } from '../../api/agents';
import { categoriesApi } from '../../api/categories';
import { ApiError } from '../../api/client';
import { customersApi } from '../../api/customers';
import { ticketsApi } from '../../api/tickets';
import { ticketDetail, ticketEscalation } from '../../test/fixtures';
import { TicketDetailPage } from './TicketDetailPage';

vi.mock('../../api/tickets', () => ({
  ticketsApi: { getTicket: vi.fn(), getEscalations: vi.fn(), escalate: vi.fn() },
}));
vi.mock('../../api/agents', () => ({ agentsApi: { getAgents: vi.fn() } }));
vi.mock('../../api/categories', () => ({ categoriesApi: { getCategories: vi.fn() } }));
vi.mock('../../api/customers', () => ({ customersApi: { getCustomers: vi.fn() } }));

const api = vi.mocked(ticketsApi);

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/tickets/12']}>
      <Routes>
        <Route path="/tickets/:id" element={<TicketDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

async function escalate(reason: string) {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Reason'), reason);
  await user.type(screen.getByLabelText('Escalated by'), 'Team Lead');
  await user.click(screen.getByRole('button', { name: 'Escalate to High' }));
}

const priorityShown = () =>
  within(screen.getByText('Priority', { selector: 'dt' }).parentElement!).getByText(/Low|Medium|High|Critical/);

describe('TicketDetailPage escalation', () => {
  beforeEach(() => {
    api.getTicket.mockResolvedValue(ticketDetail({ priority: 'Medium' }));
    api.getEscalations.mockResolvedValue([]);
    vi.mocked(agentsApi.getAgents).mockResolvedValue([]);
    vi.mocked(categoriesApi.getCategories).mockResolvedValue([]);
    vi.mocked(customersApi.getCustomers).mockResolvedValue([]);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('keeps the ticket as it was when the server refuses with 409', async () => {
    api.escalate.mockRejectedValue(new ApiError(409, 'Ticket TCK-0012 is Resolved and cannot be escalated.'));
    renderPage();

    await escalate('Customer cannot pay');

    expect(await screen.findByRole('alert')).toHaveTextContent('Ticket TCK-0012 is Resolved and cannot be escalated.');
    expect(priorityShown()).toHaveTextContent('Medium');
    expect(screen.getByText('Not escalated yet.')).toBeInTheDocument();
  });

  it('shows the escalated ticket and its history without reloading either', async () => {
    api.escalate.mockResolvedValue({
      ticket: ticketDetail({ priority: 'High' }),
      escalation: ticketEscalation({ reason: 'Customer cannot pay' }),
    });
    renderPage();

    await escalate('Customer cannot pay');

    expect(await screen.findByText('Medium → High')).toBeInTheDocument();
    expect(screen.getByText('Customer cannot pay')).toBeInTheDocument();
    expect(priorityShown()).toHaveTextContent('High');
    expect(api.getTicket).toHaveBeenCalledTimes(1);
    expect(api.getEscalations).toHaveBeenCalledTimes(1);
  });
});
