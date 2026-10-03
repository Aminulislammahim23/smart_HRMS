import type { ApiResponse } from '../types/api'
import type { AuthUser, ChangePasswordRequest, LoginRequest, LoginResult } from '../types/auth'
import { api, unwrap, unwrapMessage } from './api'

export function login(data: LoginRequest): Promise<LoginResult> {
  return unwrap(api.post<ApiResponse<LoginResult>>('/auth/login', data))
}

/** The signed-in user (validates the stored token). */
export function getCurrentUser(signal?: AbortSignal): Promise<AuthUser> {
  return unwrap(api.get<ApiResponse<AuthUser>>('/auth/me', { signal }))
}

/** Ends every existing session of this user on the server; sign in again afterwards. */
export function changePassword(data: ChangePasswordRequest): Promise<string> {
  return unwrapMessage(api.post<ApiResponse<unknown>>('/auth/change-password', data))
}
