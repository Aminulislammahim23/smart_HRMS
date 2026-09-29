import type { AttendanceStatus } from '../../types/attendance'

/** DaisyUI badge colour per backend status. */
export const STATUS_BADGE: Record<AttendanceStatus, string> = {
  Present: 'badge-success',
  Late: 'badge-warning',
  Absent: 'badge-error',
  HalfDay: 'badge-info',
  Leave: 'badge-secondary',
}

/** Calendar cell colour per backend status. */
export const STATUS_CELL: Record<AttendanceStatus, string> = {
  Present: 'bg-success/20 border-success/50',
  Late: 'bg-warning/20 border-warning/50',
  Absent: 'bg-error/20 border-error/50',
  HalfDay: 'bg-info/20 border-info/50',
  Leave: 'bg-secondary/20 border-secondary/50',
}
