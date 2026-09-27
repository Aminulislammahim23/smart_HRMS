import type { ReactNode } from 'react'
import type { AuthState } from '../types/auth'
import { AuthContext } from './authContext'

/**
 * The SmartHRMS API has no authentication yet, so every visitor is treated as signed in and no token is stored.
 * When the backend adds sign-in, this provider becomes the one place that holds the session.
 */
const NO_AUTH: AuthState = { isAuthEnabled: false, isAuthenticated: true }

export function AuthProvider({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={NO_AUTH}>{children}</AuthContext.Provider>
}
