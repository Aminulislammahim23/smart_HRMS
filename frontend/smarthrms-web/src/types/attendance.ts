/** AttendanceStatus names, exactly as the backend serializes them. */
export const ATTENDANCE_STATUSES = ['Present', 'Late', 'Absent', 'HalfDay', 'Leave'] as const
export type AttendanceStatus = (typeof ATTENDANCE_STATUSES)[number]

/**
 * AttendanceDto. Dates are "yyyy-MM-dd" and times "HH:mm:ss", both in the office time zone configured on the server
 * (not the browser's), so they are displayed as-is and never converted.
 */
export interface Attendance {
  id: string
  employeeId: string
  employeeCode: string
  employeeName: string
  attendanceDate: string
  checkInTime: string | null
  checkOutTime: string | null
  /** Calculated by the server; null until both times exist. */
  workingMinutes: number | null
  status: AttendanceStatus
  remarks: string | null
  createdAt: string
  updatedAt: string | null
}

/** CreateAttendanceDto (manual record by HR). The server calculates working minutes. */
export interface CreateAttendanceRequest {
  employeeId: string
  attendanceDate: string
  status: AttendanceStatus
  checkInTime: string | null
  checkOutTime: string | null
  remarks: string | null
}

/** UpdateAttendanceDto: full update; the employee and date can't change. */
export interface UpdateAttendanceRequest {
  status: AttendanceStatus
  checkInTime: string | null
  checkOutTime: string | null
  remarks: string | null
}

/** CheckInDto / CheckOutDto. The server uses its own clock; attendanceDate, when sent, must be today. */
export interface CheckInOutRequest {
  employeeId: string
  remarks?: string | null
}

/** Query filters shared by GET /api/attendance and GET /api/employees/{id}/attendance. Use date OR a range. */
export interface AttendanceQuery {
  date?: string
  startDate?: string
  endDate?: string
  status?: AttendanceStatus
}

/** GET /api/attendance adds an employee filter. */
export interface AttendanceListQuery extends AttendanceQuery {
  employeeId?: string
}

export function isAttendanceStatus(value: string): value is AttendanceStatus {
  return ATTENDANCE_STATUSES.some((status) => status === value)
}
