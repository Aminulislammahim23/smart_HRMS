import type { ApiResponse } from '../types/api'
import type { CreateDesignationRequest, Designation, UpdateDesignationRequest } from '../types/designation'
import { api, unwrap, unwrapMessage } from './api'

/** GET /api/designations: active and inactive designations, ordered by name. */
export function getDesignations(signal?: AbortSignal): Promise<Designation[]> {
  return unwrap(api.get<ApiResponse<Designation[]>>('/designations', { signal }))
}

export function getDesignationById(id: string, signal?: AbortSignal): Promise<Designation> {
  return unwrap(api.get<ApiResponse<Designation>>(`/designations/${id}`, { signal }))
}

export function createDesignation(data: CreateDesignationRequest): Promise<Designation> {
  return unwrap(api.post<ApiResponse<Designation>>('/designations', data))
}

export function updateDesignation(id: string, data: UpdateDesignationRequest): Promise<Designation> {
  return unwrap(api.put<ApiResponse<Designation>>(`/designations/${id}`, data))
}

/** Soft delete. The backend answers 409 while active or on-leave employees are assigned. */
export function deactivateDesignation(id: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`/designations/${id}`))
}
