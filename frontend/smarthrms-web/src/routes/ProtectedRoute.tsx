import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

/** Guards the application pages. Always passes today because the backend has no authentication yet. */
export function ProtectedRoute() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()

  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace state={{ from: location }} />
}
