import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { RequireAuth } from '../../auth/RequireAuth';
import { clearSession, saveSession } from '../../auth/session';
import { session } from '../../test/fixtures';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn() } }));

const login = vi.mocked(authApi.login);

function ProtectedPage() {
  const location = useLocation();
  return <p>Protected page at {location.pathname + location.search}</p>;
}

function renderApp(startAt: string) {
  render(
    <MemoryRouter initialEntries={[startAt]}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="*"
          element={
            <RequireAuth>
              <ProtectedPage />
            </RequireAuth>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

async function signIn(email: string, password: string) {
  const user = userEvent.setup();
  if (email) await user.type(screen.getByLabelText('Email'), email);
  if (password) await user.type(screen.getByLabelText('Password'), password);
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('signing in', () => {
  afterEach(() => {
    vi.clearAllMocks();
    clearSession();
  });

  it('sends a visitor who is not signed in to the login page', () => {
    renderApp('/tickets/5');

    expect(screen.getByRole('heading', { name: 'Sign in to Northwind Support' })).toBeInTheDocument();
    expect(screen.queryByText(/Protected page/)).not.toBeInTheDocument();
  });

  it('returns them to the page they wanted once they sign in', async () => {
    login.mockResolvedValue(session());
    renderApp('/tickets/5?tab=history');

    await signIn('alex.turner@northwind-support.example', 'LocalDev-Only-1');

    expect(await screen.findByText('Protected page at /tickets/5?tab=history')).toBeInTheDocument();
    expect(login).toHaveBeenCalledWith('alex.turner@northwind-support.example', 'LocalDev-Only-1');
  });

  it('checks the fields before calling the API', async () => {
    renderApp('/login');

    await signIn('not-an-email', '');

    expect(screen.getByText('Enter the email address you sign in with.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('says so when the credentials are rejected, and stays on the page', async () => {
    login.mockRejectedValue(new ApiError(401, 'Email or password is incorrect.'));
    renderApp('/login');

    await signIn('alex.turner@northwind-support.example', 'wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  });

  it('sends the agent back to sign in when their session ends, for example on a 401', () => {
    saveSession(session());
    renderApp('/tickets/5');
    expect(screen.getByText('Protected page at /tickets/5')).toBeInTheDocument();

    act(() => clearSession());

    expect(screen.getByRole('heading', { name: 'Sign in to Northwind Support' })).toBeInTheDocument();
  });
});
