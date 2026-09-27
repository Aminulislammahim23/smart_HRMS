import type { ApiResponse } from '../types/api'
import type { CreateDepartmentRequest, Department, UpdateDepartmentRequest } from '../types/department'
import { api, unwrap, unwrapMessage } from './api'

/** GET /api/departments: active and inactive departments, ordered by name. */
export function getDepartments(signal?: AbortSignal): Promise<Department[]> {
  return unwrap(api.get<ApiResponse<Department[]>>('/departments', { signal }))
}

export function getDepartmentById(id: string, signal?: AbortSignal): Promise<Department> {
  return unwrap(api.get<ApiResponse<Department>>(`/departments/${id}`, { signal }))
}

export function createDepartment(data: CreateDepartmentRequest): Promise<Department> {
  return unwrap(api.post<ApiResponse<Department>>('/departments', data))
}

export function updateDepartment(id: string, data: UpdateDepartmentRequest): Promise<Department> {
  return unwrap(api.put<ApiResponse<Department>>(`/departments/${id}`, data))
}

/** Soft delete. The backend answers 409 while active or on-leave employees are assigned. */
export function deactivateDepartment(id: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`/departments/${id}`))
}
