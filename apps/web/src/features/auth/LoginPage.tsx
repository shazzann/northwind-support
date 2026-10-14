import { useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { toErrorMessage } from '../../api/client';
import { saveSession } from '../../auth/session';
import { useSession } from '../../auth/useSession';
import { Field } from '../../components/Field';

interface Credentials {
  email: string;
  password: string;
}

type CredentialErrors = Partial<Record<keyof Credentials, string>>;

/** Agents sign in here; afterwards they go back to the page they were trying to reach. */
export function LoginPage() {
  const session = useSession();
  const location = useLocation();

  const [form, setForm] = useState<Credentials>({ email: '', password: '' });
  const [errors, setErrors] = useState<CredentialErrors>({});
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  // Signed in - already, or just now - so on to where they were going.
  if (session) {
    return <Navigate to={returnPath(location.state)} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSigningIn(true);

    try {
      saveSession(await authApi.login(form.email.trim(), form.password));
    } catch (caught) {
      setSubmitError(toErrorMessage(caught, 'Could not sign in.'));
      setIsSigningIn(false);
    }
  };

  return (
    <main className="login">
      <form className="card form login__card" onSubmit={submit} noValidate aria-labelledby="login-heading">
        <h1 id="login-heading">Sign in to Northwind Support</h1>

        <Field id="login-email" label="Email" error={errors.email}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="email"
              autoComplete="username"
              value={form.email}
              onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="password"
              autoComplete="current-password"
              value={form.password}
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
            />
          )}
        </Field>

        {submitError && (
          <p className="field__error" role="alert">
            {submitError}
          </p>
        )}

        <div className="button-row">
          <button type="submit" className="button button--primary" disabled={isSigningIn}>
            {isSigningIn ? 'Signing in...' : 'Sign in'}
          </button>
        </div>
      </form>
    </main>
  );
}

function validate(form: Credentials): CredentialErrors {
  const errors: CredentialErrors = {};

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
    errors.email = 'Enter the email address you sign in with.';
  }

  if (form.password === '') {
    errors.password = 'Enter your password.';
  }

  return errors;
}

/** Where RequireAuth was sending the visitor, or the ticket list. */
function returnPath(state: unknown): string {
  if (typeof state === 'object' && state !== null && 'from' in state) {
    const { from } = state;

    if (typeof from === 'object' && from !== null && 'pathname' in from && typeof from.pathname === 'string') {
      const search = 'search' in from && typeof from.search === 'string' ? from.search : '';
      return from.pathname + search;
    }
  }

  return '/tickets';
}
