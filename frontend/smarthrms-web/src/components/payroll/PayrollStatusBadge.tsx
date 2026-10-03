import type { PayrollRecordStatus, PayrollStatus } from '../../types/payroll'
import { enumLabel } from '../../utils/formatters'

// Neutral states use an outline: a soft neutral badge is almost invisible on the dark theme.
const PERIOD_CLASSES: Record<PayrollStatus, string> = {
  Draft: 'badge-outline',
  Calculated: 'badge-soft badge-info',
  PendingApproval: 'badge-soft badge-warning',
  Approved: 'badge-soft badge-primary',
  Paid: 'badge-soft badge-success',
  Cancelled: 'badge-outline badge-error',
}

const RECORD_CLASSES: Record<PayrollRecordStatus, string> = {
  Calculated: 'badge-soft badge-info',
  NeedsReview: 'badge-soft badge-error',
  Approved: 'badge-soft badge-primary',
  Paid: 'badge-soft badge-success',
  Cancelled: 'badge-outline badge-error',
}

export function PayrollStatusBadge({ status }: { status: PayrollStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${PERIOD_CLASSES[status]}`}>{enumLabel(status)}</span>
}

export function PayrollRecordStatusBadge({ status }: { status: PayrollRecordStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${RECORD_CLASSES[status]}`}>{enumLabel(status)}</span>
}
