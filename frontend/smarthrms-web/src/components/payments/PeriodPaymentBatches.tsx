import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { useApi } from '../../hooks/useApi'
import { getPaymentBatches } from '../../services/paymentService'
import { enumLabel, formatAmount, formatDate } from '../../utils/formatters'
import { ErrorState } from '../common/ErrorState'
import { PaymentBatchStatusBadge } from './PaymentBadges'

/** The payment batches of one finalized payroll (newest first). */
export function PeriodPaymentBatches({ periodId }: { periodId: string }) {
  const load = useCallback((signal: AbortSignal) => getPaymentBatches({ payrollPeriodId: periodId, pageSize: 100 }, signal), [periodId])
  const { data, error, reload } = useApi(load)

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-3">
        <h2 className="font-semibold">Payment batches</h2>
        {error ? (
          <ErrorState error={error} onRetry={reload} />
        ) : !data ? (
          <span className="loading loading-spinner loading-sm" />
        ) : data.items.length === 0 ? (
          <p className="text-sm text-base-content/60">No payment batch yet. An Admin creates one to pay the salaries of this payroll.</p>
        ) : (
          <ul className="divide-y divide-base-200 text-sm">
            {data.items.map((b) => (
              <li key={b.id} className="flex flex-col gap-1 py-2 sm:flex-row sm:items-center sm:justify-between">
                <Link to={`/payments/batches/${b.id}`} className="font-medium hover:text-primary">
                  {b.batchNumber}
                </Link>
                <span className="text-base-content/70">
                  {enumLabel(b.paymentMethod)} · {formatDate(b.paymentDate)} · {b.totalEmployees} employees · <span className="tabular-nums">{formatAmount(b.totalAmount)}</span>
                </span>
                <PaymentBatchStatusBadge status={b.status} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
