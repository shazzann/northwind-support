import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { page, ticketListItem } from '../../test/fixtures';
import { TicketListPage } from './TicketListPage';

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

const fetchMock = vi.fn(async (input: RequestInfo | URL) =>
  String(input).startsWith('/api/tickets') ? json(page([ticketListItem()])) : json([]),
);

/** The query strings of every GET /api/tickets the page has sent, oldest first. */
const ticketRequests = () =>
  fetchMock.mock.calls
    .map(([input]) => String(input))
    .filter((url) => url.startsWith('/api/tickets?'))
    .map((url) => new URLSearchParams(url.split('?')[1]));

describe('TicketListPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockClear();
  });

  // DEFECT-117: picking a status sent no request; the next search then carried it along.
  it('requests the tickets for a status as soon as it is picked', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <TicketListPage />
      </MemoryRouter>,
    );

    await screen.findByText('TCK-0012');
    expect(ticketRequests()).toHaveLength(1);

    await user.selectOptions(screen.getByLabelText('Status'), 'InProgress');

    await waitFor(() => expect(ticketRequests()).toHaveLength(2));
    expect(ticketRequests()[1].get('status')).toBe('InProgress');
    expect(ticketRequests()[1].get('search')).toBeNull();
  });
});
