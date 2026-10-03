import type { ApiResponse } from '../types/api'
import type { CreateLeaveRequest, LeaveQuery, LeaveRequest } from '../types/leave'
import { api, unwrap } from './api'

function params(query: LeaveQuery) {
  return Object.fromEntries(Object.entries(query).filter(([, value]) => value !== undefined && value !== ''))
}

/** Requests the signed-in user may see (the server applies the scope). */
export function getLeaveRequests(query: LeaveQuery, signal?: AbortSignal): Promise<LeaveRequest[]> {
  return unwrap(api.get<ApiResponse<LeaveRequest[]>>('/leave/requests', { params: params(query), signal }))
}

export function applyForLeave(data: CreateLeaveRequest): Promise<LeaveRequest> {
  return unwrap(api.post<ApiResponse<LeaveRequest>>('/leave/requests', data))
}

export function approveLeave(id: string, comment: string | null): Promise<LeaveRequest> {
  return unwrap(api.post<ApiResponse<LeaveRequest>>(`/leave/requests/${id}/approve`, { comment }))
}

export function rejectLeave(id: string, comment: string | null): Promise<LeaveRequest> {
  return unwrap(api.post<ApiResponse<LeaveRequest>>(`/leave/requests/${id}/reject`, { comment }))
}

export function cancelLeave(id: string, comment: string | null): Promise<LeaveRequest> {
  return unwrap(api.post<ApiResponse<LeaveRequest>>(`/leave/requests/${id}/cancel`, { comment }))
}
