import type { AttendanceStatus } from '../../types/attendance'
import { enumLabel } from '../../utils/formatters'
import { STATUS_BADGE } from './attendanceStyles'

export function AttendanceStatusBadge({ status }: { status: AttendanceStatus }) {
  return <span className={`badge badge-soft badge-sm whitespace-nowrap ${STATUS_BADGE[status]}`}>{enumLabel(status)}</span>
}
