import { Link, NavLink, Outlet } from 'react-router-dom';
import { clearSession } from './auth/session';
import { useSession } from './auth/useSession';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const session = useSession();

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
          <NavLink to="/reports/sla">SLA report</NavLink>
        </nav>

        {session && (
          <div className="app__account">
            <span>
              Signed in as <strong>{session.agent.fullName}</strong>
            </span>
            {/* Ending the session is enough: RequireAuth then sends the page to sign-in. */}
            <button type="button" className="button button--ghost" onClick={clearSession}>
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
