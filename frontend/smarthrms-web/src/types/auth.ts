/**
 * The backend has no authentication yet: no login endpoint, tokens, users or roles.
 * Routes and the layout already read this state so that real sign-in can be added in one place
 * (AuthProvider) once the backend supports it.
 */
export interface AuthState {
  /** False until the backend provides authentication. */
  isAuthEnabled: boolean
  isAuthenticated: boolean
}
