import type { EmployeeStatus } from '../../types/employee'
import { enumLabel } from '../../utils/formatters'

// Neutral states use an outline: a soft neutral badge is almost invisible on the dark theme.
const STATUS_CLASSES: Record<EmployeeStatus, string> = {
  Active: 'badge-soft badge-success',
  OnLeave: 'badge-soft badge-warning',
  Inactive: 'badge-outline',
  Resigned: 'badge-outline badge-secondary',
  Terminated: 'badge-soft badge-error',
}

export function EmployeeStatusBadge({ status }: { status: EmployeeStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${STATUS_CLASSES[status]}`}>{enumLabel(status)}</span>
}

export function ActiveBadge({ isActive }: { isActive: boolean }) {
  return <span className={`badge badge-sm ${isActive ? 'badge-soft badge-success' : 'badge-outline'}`}>{isActive ? 'Active' : 'Inactive'}</span>
}
