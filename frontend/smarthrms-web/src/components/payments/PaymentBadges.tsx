import type { PaymentBatchStatus, PaymentTransactionStatus } from '../../types/payment'
import { enumLabel } from '../../utils/formatters'

// Neutral states use an outline: a soft neutral badge is almost invisible on the dark theme.
const PAYMENT_CLASSES: Record<PaymentTransactionStatus, string> = {
  Pending: 'badge-outline',
  Processing: 'badge-soft badge-info',
  Paid: 'badge-soft badge-success',
  Failed: 'badge-soft badge-error',
  Cancelled: 'badge-outline badge-error',
}

const BATCH_CLASSES: Record<PaymentBatchStatus, string> = {
  Pending: 'badge-outline',
  Processing: 'badge-soft badge-info',
  PartiallyPaid: 'badge-soft badge-warning',
  Paid: 'badge-soft badge-success',
  Failed: 'badge-soft badge-error',
  Cancelled: 'badge-outline badge-error',
}

export function PaymentTransactionStatusBadge({ status }: { status: PaymentTransactionStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${PAYMENT_CLASSES[status]}`}>{status}</span>
}

export function PaymentBatchStatusBadge({ status }: { status: PaymentBatchStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${BATCH_CLASSES[status]}`}>{enumLabel(status)}</span>
}
