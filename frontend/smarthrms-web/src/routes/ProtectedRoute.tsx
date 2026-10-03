import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Loading } from '../components/common/Loading'
import { useAuth } from '../hooks/useAuth'
import Forbidden from '../pages/Forbidden'
import type { UserRole } from '../types/auth'

/** Signed-in users only; everyone else goes to the sign-in page and comes back afterwards. */
export function ProtectedRoute() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'loading') {
    return (
      <div className="grid min-h-screen place-items-center">
        <Loading label="Restoring your session…" />
      </div>
    )
  }

  return status === 'signedIn' ? <Outlet /> : <Navigate to="/login" replace state={{ from: location }} />
}

/**
 * Pages for some roles only. This only decides what the browser shows; the API refuses the same requests for
 * other roles, so a hidden page can't be used by typing its URL.
 */
export function RoleRoute({ roles, requireEmployee = false }: { roles?: UserRole[]; requireEmployee?: boolean }) {
  const { user, hasRole } = useAuth()

  const allowed = (!roles || hasRole(...roles)) && (!requireEmployee || !!user?.employeeId)
  return allowed ? <Outlet /> : <Forbidden reason={requireEmployee && !user?.employeeId ? 'noEmployee' : 'role'} />
}
