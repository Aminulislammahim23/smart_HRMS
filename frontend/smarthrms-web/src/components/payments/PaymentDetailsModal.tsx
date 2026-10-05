import { ReceiptText } from 'lucide-react'
import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { useApi } from '../../hooks/useApi'
import { getPayment } from '../../services/paymentService'
import { enumLabel, formatAmount, formatDate, formatDateTime } from '../../utils/formatters'
import { DetailList } from '../common/DetailList'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { Modal } from '../common/Modal'
import { PaymentTransactionStatusBadge } from './PaymentBadges'

/** One payment with its status history. Employees can open only their own; the server answers 404 otherwise. */
export function PaymentDetailsModal({ paymentId, onClose }: { paymentId: string; onClose: () => void }) {
  const load = useCallback((signal: AbortSignal) => getPayment(paymentId, signal), [paymentId])
  const { data: payment, error, loading, reload } = useApi(load)

  return (
    <Modal open title="Payment details" onClose={onClose} size="lg">
      {error ? (
        <ErrorState error={error} onRetry={reload} />
      ) : loading && !payment ? (
        <Loading label="Loading payment…" />
      ) : payment ? (
        <div className="flex flex-col gap-5">
          <DetailList
            columns={3}
            items={[
              { label: 'Employee', value: `${payment.employeeName} (${payment.employeeCode})` },
              { label: 'Payroll period', value: payment.periodName },
              { label: 'Batch', value: payment.batchNumber },
              { label: 'Amount', value: <span className="font-semibold tabular-nums">{formatAmount(payment.amount)}</span> },
              { label: 'Method', value: enumLabel(payment.paymentMethod) },
              { label: 'Status', value: <PaymentTransactionStatusBadge status={payment.status} /> },
              { label: payment.status === 'Paid' ? 'Paid on' : 'Planned date', value: formatDate(payment.paymentDate) },
              { label: 'Transaction reference', value: payment.transactionReference },
              { label: 'Failure reason', value: payment.failureReason },
            ]}
          />
          {payment.payslipId && (
            <Link to={`/payroll/payslips/${payment.payslipId}`} className="btn btn-sm self-start">
              <ReceiptText className="size-4" /> Payslip
            </Link>
          )}
          <div>
            <h4 className="mb-2 font-semibold">Status history</h4>
            <ul className="timeline timeline-vertical timeline-compact -ml-2">
              {(payment.history ?? []).map((h, index, all) => (
                <li key={`${h.changedAt}-${index}`}>
                  {index > 0 && <hr className="bg-primary" />}
                  <div className="timeline-middle">
                    <span className={`block size-3 rounded-full ${h.newStatus === 'Failed' || h.newStatus === 'Cancelled' ? 'bg-error' : 'bg-primary'}`} />
                  </div>
                  <div className="timeline-end mb-3">
                    <p className="text-sm font-medium">{h.previousStatus ? `${h.previousStatus} → ${h.newStatus}` : `Created (${h.newStatus})`}</p>
                    <p className="text-xs text-base-content/60">
                      {formatDateTime(h.changedAt)}
                      {h.changedBy ? ` · ${h.changedBy}` : ''}
                    </p>
                    {h.reason && <p className="text-xs text-base-content/70">{h.reason}</p>}
                  </div>
                  {index < all.length - 1 && <hr className="bg-primary" />}
                </li>
              ))}
            </ul>
          </div>
        </div>
      ) : null}
    </Modal>
  )
}
