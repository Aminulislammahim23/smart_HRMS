import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { getCurrentUser, login as loginRequest } from '../services/authService'
import { isCancelled } from '../services/api'
import { tokenStore } from '../services/tokenStore'
import type { AuthState, AuthUser, LoginRequest, UserRole } from '../types/auth'
import { AuthContext } from './authContext'

const EXPIRED = 'Your session has ended. Please sign in again.'

/**
 * The one place that holds the session. On start it validates a stored token with /auth/me; afterwards any 401 from
 * the API (expired token, account deactivated, role or password changed) signs the user out.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [status, setStatus] = useState<AuthState['status']>(() => (tokenStore.get() ? 'loading' : 'signedOut'))
  const [signedOutReason, setSignedOutReason] = useState<string | null>(null)

  const logout = useCallback((reason?: string) => {
    tokenStore.clear()
    setUser(null)
    setStatus('signedOut')
    setSignedOutReason(reason ?? null)
  }, [])

  useEffect(() => tokenStore.onUnauthorized(() => logout(EXPIRED)), [logout])

  // Restore the session after a reload.
  useEffect(() => {
    if (status !== 'loading') return
    const controller = new AbortController()
    getCurrentUser(controller.signal).then(
      (current) => {
        setUser(current)
        setStatus('signedIn')
      },
      (error: unknown) => {
        if (!isCancelled(error)) logout(EXPIRED)
      },
    )
    return () => controller.abort()
  }, [status, logout])

  const login = useCallback(async (request: LoginRequest) => {
    const result = await loginRequest(request)
    tokenStore.set(result.accessToken, result.expiresAt)
    setUser(result.user)
    setStatus('signedIn')
    setSignedOutReason(null)
    return result.user
  }, [])

  const value = useMemo<AuthState>(
    () => ({
      status,
      user,
      isAuthenticated: status === 'signedIn' && user !== null,
      signedOutReason,
      login,
      logout,
      hasRole: (...roles: UserRole[]) => user !== null && roles.includes(user.role),
    }),
    [status, user, signedOutReason, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
