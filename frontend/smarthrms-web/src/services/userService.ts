import type { ApiResponse } from '../types/api'
import type { CreateUserRequest, UpdateUserRequest, User } from '../types/user'
import { api, unwrap, unwrapMessage } from './api'

export function getUsers(signal?: AbortSignal): Promise<User[]> {
  return unwrap(api.get<ApiResponse<User[]>>('/users', { signal }))
}

export function createUser(data: CreateUserRequest): Promise<User> {
  return unwrap(api.post<ApiResponse<User>>('/users', data))
}

/** Role, active flag and employee link. The user's existing sessions end. */
export function updateUser(id: string, data: UpdateUserRequest): Promise<User> {
  return unwrap(api.put<ApiResponse<User>>(`/users/${id}`, data))
}

/** Sets a new password and unlocks the account. */
export function resetPassword(id: string, newPassword: string): Promise<string> {
  return unwrapMessage(api.post<ApiResponse<unknown>>(`/users/${id}/reset-password`, { newPassword }))
}
