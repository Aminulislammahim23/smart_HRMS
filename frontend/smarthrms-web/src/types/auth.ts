/** UserRole names, exactly as the backend serializes them. */
export const USER_ROLES = ['Employee', 'Manager', 'HR', 'Admin'] as const
export type UserRole = (typeof USER_ROLES)[number]

/** CurrentUserDto: who is signed in. */
export interface AuthUser {
  id: string
  username: string
  role: UserRole
  /** The employee this account belongs to; null for an account without one (e.g. an administrator). */
  employeeId: string | null
  employeeCode: string | null
  displayName: string
}

/** LoginResultDto */
export interface LoginResult {
  accessToken: string
  /** UTC instant. */
  expiresAt: string
  user: AuthUser
}

export interface LoginRequest {
  username: string
  password: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

/**
 * The session as the UI sees it. Role checks here only decide what is shown; the API checks every request again and
 * is the real authority.
 */
export interface AuthState {
  status: 'loading' | 'signedIn' | 'signedOut'
  user: AuthUser | null
  isAuthenticated: boolean
  /** Why the last session ended (expired, signed out elsewhere), shown on the sign-in page. */
  signedOutReason: string | null
  login: (request: LoginRequest) => Promise<AuthUser>
  logout: (reason?: string) => void
  hasRole: (...roles: UserRole[]) => boolean
}
