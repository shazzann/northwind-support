import type { LoginResponse } from '../types/api';

/** Who is signed in, and the token that proves it. Exactly what the login endpoint returned. */
export type Session = LoginResponse;

/**
 * Kept in sessionStorage: it survives a reload but not closing the tab, and is never sent
 * anywhere by the browser on its own (unlike a cookie). Like any web storage it is readable
 * by script on the page, so an XSS hole would expose it - see the README for the trade-off.
 */
const storageKey = 'northwind-support.session';

const listeners = new Set<() => void>();

let current: Session | null = load();

/**
 * The session to send with a request, or null when nobody is signed in. A session found to
 * have expired is ended here, before it costs a round trip to be refused.
 */
export function getSession(): Session | null {
  if (current && isExpired(current)) {
    clearSession();
  }

  return current;
}

/** The session as last saved, with no side effects - safe to read while React renders. */
export function currentSession(): Session | null {
  return current;
}

export function saveSession(session: Session): void {
  current = session;
  write(JSON.stringify(session));
  notify();
}

export function clearSession(): void {
  if (current === null) {
    return;
  }

  current = null;
  write(null);
  notify();
}

/** For useSyncExternalStore: calls back whenever the session starts or ends. */
export function subscribeToSession(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function notify() {
  listeners.forEach((listener) => listener());
}

function isExpired(session: Session): boolean {
  return Date.parse(session.expiresAtUtc) <= Date.now();
}

function load(): Session | null {
  try {
    const stored = sessionStorage.getItem(storageKey);
    const session = stored ? (JSON.parse(stored) as Session) : null;
    return session && !isExpired(session) ? session : null;
  } catch {
    // Storage blocked or the value unreadable: start signed out.
    return null;
  }
}

function write(value: string | null) {
  try {
    if (value === null) {
      sessionStorage.removeItem(storageKey);
    } else {
      sessionStorage.setItem(storageKey, value);
    }
  } catch {
    // Storage blocked: the session still lives in memory for this page.
  }
}
