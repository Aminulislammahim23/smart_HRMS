import { Lock } from 'lucide-react'
import type { PaymentStatus, PayrollRecordStatus, PayrollStatus } from '../../types/payroll'
import { enumLabel } from '../../utils/formatters'

// Neutral states use an outline: a soft neutral badge is almost invisible on the dark theme.
const PERIOD_CLASSES: Record<PayrollStatus, string> = {
  Draft: 'badge-outline',
  Calculated: 'badge-soft badge-info',
  PendingApproval: 'badge-soft badge-warning',
  Approved: 'badge-soft badge-primary',
  Finalized: 'badge-soft badge-accent',
  Paid: 'badge-soft badge-success',
  Cancelled: 'badge-outline badge-error',
}

const RECORD_CLASSES: Record<PayrollRecordStatus, string> = {
  Calculated: 'badge-soft badge-info',
  NeedsReview: 'badge-soft badge-error',
  Approved: 'badge-soft badge-primary',
  Finalized: 'badge-soft badge-accent',
  Paid: 'badge-soft badge-success',
  Cancelled: 'badge-outline badge-error',
}

export function PayrollStatusBadge({ status }: { status: PayrollStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${PERIOD_CLASSES[status]}`}>{enumLabel(status)}</span>
}

export function PayrollRecordStatusBadge({ status }: { status: PayrollRecordStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${RECORD_CLASSES[status]}`}>{enumLabel(status)}</span>
}

/** Payslip payment: Paid, Unpaid, or "Not issued" while no payslip exists yet. */
export function PaymentStatusBadge({ status }: { status: PaymentStatus | null }) {
  if (!status) return <span className="badge badge-sm badge-outline whitespace-nowrap">Not issued</span>
  return <span className={`badge badge-sm whitespace-nowrap ${status === 'Paid' ? 'badge-soft badge-success' : 'badge-soft badge-warning'}`}>{status}</span>
}

/** Shown on finalized payroll: no payroll value can change any more. */
export function LockedBadge() {
  return (
    <span className="badge badge-sm badge-outline gap-1 whitespace-nowrap" title="Finalized payroll is locked and can't be edited">
      <Lock className="size-3" /> Locked
    </span>
  )
}
