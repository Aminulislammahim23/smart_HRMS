export const LEAVE_TYPES = ['Annual', 'Sick', 'Casual', 'Unpaid'] as const
export type LeaveType = (typeof LEAVE_TYPES)[number]

export const LEAVE_STATUSES = ['Pending', 'Approved', 'Rejected', 'Cancelled'] as const
export type LeaveStatus = (typeof LEAVE_STATUSES)[number]

/** LeaveRequestDto. Dates are "yyyy-MM-dd" calendar dates. */
export interface LeaveRequest {
  id: string
  employeeId: string
  employeeCode: string
  employeeName: string
  leaveType: LeaveType
  isPaid: boolean
  startDate: string
  endDate: string
  /** Working days (weekends excluded), calculated by the server. */
  totalDays: number
  reason: string | null
  status: LeaveStatus
  requestedBy: string | null
  reviewedBy: string | null
  reviewedAt: string | null
  reviewComment: string | null
  /** Computed by the server for the signed-in user. */
  canReview: boolean
  canCancel: boolean
  createdAt: string
  updatedAt: string | null
}

export interface CreateLeaveRequest {
  /** Only honoured for HR/Admin; everyone else always applies for themselves. */
  employeeId?: string
  leaveType: LeaveType
  startDate: string
  endDate: string
  reason: string | null
}

export type LeaveScope = 'mine' | 'team' | 'all'

export interface LeaveQuery {
  scope?: LeaveScope
  employeeId?: string
  status?: LeaveStatus
  leaveType?: LeaveType
  startDate?: string
  endDate?: string
}

export function isLeaveStatus(value: string): value is LeaveStatus {
  return LEAVE_STATUSES.some((status) => status === value)
}

export function isLeaveType(value: string): value is LeaveType {
  return LEAVE_TYPES.some((type) => type === value)
}
