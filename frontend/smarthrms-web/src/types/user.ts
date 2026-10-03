import type { UserRole } from './auth'

/** UserDto (Admin user management). */
export interface User {
  id: string
  username: string
  role: UserRole
  isActive: boolean
  employeeId: string | null
  employeeCode: string | null
  employeeName: string | null
  lastLoginAt: string | null
  /** Set while the account is locked after failed sign-ins. */
  lockoutEndAt: string | null
  createdAt: string
  updatedAt: string | null
}

export interface CreateUserRequest {
  username: string
  password: string
  role: UserRole
  employeeId: string | null
}

export interface UpdateUserRequest {
  role: UserRole
  isActive: boolean
  employeeId: string | null
}
