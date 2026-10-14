import { useSyncExternalStore } from 'react';
import { currentSession, subscribeToSession } from './session';

/** The current session; re-renders when someone signs in or out, or a 401 ends the session. */
export function useSession() {
  return useSyncExternalStore(subscribeToSession, currentSession);
}
