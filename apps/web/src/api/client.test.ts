import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, currentSession, saveSession } from '../auth/session';
import { session } from '../test/fixtures';
import { ApiError, request } from './client';

const fetchMock = vi.fn<typeof fetch>();

const sentHeaders = () => new Headers(fetchMock.mock.calls[0][1]?.headers);

describe('request', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    fetchMock.mockResolvedValue(new Response('[]', { status: 200 }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
    clearSession();
  });

  it('sends the signed-in agent token with the request', async () => {
    saveSession(session({ token: 'abc.def.ghi' }));

    await request('/tickets');

    expect(sentHeaders().get('Authorization')).toBe('Bearer abc.def.ghi');
  });

  it('sends no token when nobody is signed in', async () => {
    await request('/tickets');

    expect(sentHeaders().has('Authorization')).toBe(false);
  });

  it('does not send a token that has already expired', async () => {
    saveSession(session({ expiresAtUtc: new Date(Date.now() - 1000).toISOString() }));

    await request('/tickets');

    expect(sentHeaders().has('Authorization')).toBe(false);
    expect(currentSession()).toBeNull();
  });

  it('ends the session when the API answers 401', async () => {
    saveSession(session());
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ title: 'Unauthorized' }), { status: 401 }));

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(currentSession()).toBeNull();
  });
});
