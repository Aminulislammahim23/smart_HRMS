import type { ApiResponse } from '../types/api'
import type { CreateEmployeeRequest, Employee, UpdateEmployeeRequest } from '../types/employee'
import type { EmployeeProfile } from '../types/profile'
import { api, unwrap, unwrapMessage } from './api'

/** GET /api/employees: every employee, whatever their status. The backend has no paging or filtering. */
export function getEmployees(signal?: AbortSignal): Promise<Employee[]> {
  return unwrap(api.get<ApiResponse<Employee[]>>('/employees', { signal }))
}

export function getEmployeeById(id: string, signal?: AbortSignal): Promise<Employee> {
  return unwrap(api.get<ApiResponse<Employee>>(`/employees/${id}`, { signal }))
}

/** GET /api/employees/{id}/profile: every profile section in one response. */
export function getEmployeeProfile(id: string, signal?: AbortSignal): Promise<EmployeeProfile> {
  return unwrap(api.get<ApiResponse<EmployeeProfile>>(`/employees/${id}/profile`, { signal }))
}

export function createEmployee(data: CreateEmployeeRequest): Promise<Employee> {
  return unwrap(api.post<ApiResponse<Employee>>('/employees', data))
}

export function updateEmployee(id: string, data: UpdateEmployeeRequest): Promise<Employee> {
  return unwrap(api.put<ApiResponse<Employee>>(`/employees/${id}`, data))
}

/** DELETE /api/employees/{id} is a soft delete: it sets the status to Inactive. */
export function deactivateEmployee(id: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`/employees/${id}`))
}

/** PUT /api/employees/{id}/photo (multipart, field "photo"). Uploads or replaces the photo. */
export function uploadEmployeePhoto(id: string, photo: File): Promise<Employee> {
  const form = new FormData()
  form.append('photo', photo)
  return unwrap(api.put<ApiResponse<Employee>>(`/employees/${id}/photo`, form))
}

export function removeEmployeePhoto(id: string): Promise<Employee> {
  return unwrap(api.delete<ApiResponse<Employee>>(`/employees/${id}/photo`))
}

/** Sets (managerId) or clears (null) the employee's line manager (HR/Admin). */
export function assignManager(id: string, managerId: string | null): Promise<Employee> {
  return unwrap(api.put<ApiResponse<Employee>>(`/employees/${id}/manager`, { managerId }))
}
