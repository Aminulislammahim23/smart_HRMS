import type { ApiResponse } from '../types/api'
import type {
  Attendance,
  AttendanceListQuery,
  AttendanceQuery,
  CheckInOutRequest,
  CreateAttendanceRequest,
  UpdateAttendanceRequest,
} from '../types/attendance'
import { api, unwrap, unwrapMessage } from './api'

/** Drops empty values so they are not sent as "?status=" (the backend would treat that as "no filter" anyway). */
function params(query: AttendanceQuery & { employeeId?: string }) {
  return Object.fromEntries(Object.entries(query).filter(([, value]) => value !== undefined && value !== ''))
}

/** GET /api/attendance: newest date first, then by employee code. Always pass a date or range for large data. */
export function getAttendance(query: AttendanceListQuery, signal?: AbortSignal): Promise<Attendance[]> {
  return unwrap(api.get<ApiResponse<Attendance[]>>('/attendance', { params: params(query), signal }))
}

/** GET /api/employees/{id}/attendance (404 if the employee doesn't exist). */
export function getEmployeeAttendance(employeeId: string, query: AttendanceQuery, signal?: AbortSignal): Promise<Attendance[]> {
  return unwrap(api.get<ApiResponse<Attendance[]>>(`/employees/${employeeId}/attendance`, { params: params(query), signal }))
}

export function getAttendanceById(id: string, signal?: AbortSignal): Promise<Attendance> {
  return unwrap(api.get<ApiResponse<Attendance>>(`/attendance/${id}`, { signal }))
}

export function createAttendance(data: CreateAttendanceRequest): Promise<Attendance> {
  return unwrap(api.post<ApiResponse<Attendance>>('/attendance', data))
}

export function updateAttendance(id: string, data: UpdateAttendanceRequest): Promise<Attendance> {
  return unwrap(api.put<ApiResponse<Attendance>>(`/attendance/${id}`, data))
}

/** Permanent delete (HR correction). */
export function deleteAttendance(id: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`/attendance/${id}`))
}

/** Checks the employee in for today at the server's current office time. */
export function checkIn(data: CheckInOutRequest): Promise<Attendance> {
  return unwrap(api.post<ApiResponse<Attendance>>('/attendance/check-in', data))
}

/** Checks the employee out for today; the server calculates working minutes. */
export function checkOut(data: CheckInOutRequest): Promise<Attendance> {
  return unwrap(api.post<ApiResponse<Attendance>>('/attendance/check-out', data))
}
