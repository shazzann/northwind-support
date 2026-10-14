import { Navigate, useLocation } from 'react-router-dom';
import { useSession } from './useSession';

interface RequireAuthProps {
  children: React.ReactNode;
}

/**
 * Shows its children only to a signed-in agent. Anyone else goes to the login page, which
 * sends them back here afterwards. Because it watches the session, a 401 that ends the
 * session anywhere in the app lands here too.
 */
export function RequireAuth({ children }: RequireAuthProps) {
  const session = useSession();
  const location = useLocation();

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return children;
}
