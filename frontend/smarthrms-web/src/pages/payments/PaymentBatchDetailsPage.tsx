import { Ban, Play } from 'lucide-react'
import { useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { DetailList } from '../../components/common/DetailList'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { PaymentBatchStatusBadge } from '../../components/payments/PaymentBadges'
import { PaymentList } from '../../components/payments/PaymentList'
import { useBatchActions } from '../../components/payments/useBatchActions'
import { useApi } from '../../hooks/useApi'
import { getPaymentBatch } from '../../services/paymentService'
import { enumLabel, formatAmount, formatDate, formatDateTime } from '../../utils/formatters'

/** One payment batch: its summary and the employees' payments, each processed individually. */
export default function PaymentBatchDetailsPage() {
  const { id = '' } = useParams()
  const load = useCallback((signal: AbortSignal) => getPaymentBatch(id, signal), [id])
  const { data: batch, error, loading, reload } = useApi(load)
  const { request, dialogs } = useBatchActions(() => reload())

  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} action={<Link to="/payments/batches" className="btn btn-sm">Back to batches</Link>} /></div>
  if (loading && !batch) return <Loading label="Loading payment batch…" />
  if (!batch) return null

  const counts = batch.counts

  return (
    <>
      <PageHeader
        title={batch.batchNumber}
        description={`Salary payment of ${batch.periodName}`}
        actions={
          <>
            {batch.actions.canProcess && (
              <button type="button" className="btn btn-primary btn-sm" onClick={() => request('process', batch)}>
                <Play className="size-4" /> Start processing
              </button>
            )}
            {batch.actions.canCancel && (
              <button type="button" className="btn btn-ghost btn-sm text-error" onClick={() => request('cancel', batch)}>
                <Ban className="size-4" /> Cancel batch
              </button>
            )}
          </>
        }
      />

      <div className="flex flex-col gap-6">
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body gap-4">
            <DetailList
              columns={3}
              items={[
                { label: 'Batch number', value: batch.batchNumber },
                { label: 'Payroll period', value: <Link to={`/payroll/${batch.payrollPeriodId}`} className="link">{batch.periodName}</Link> },
                { label: 'Batch status', value: <PaymentBatchStatusBadge status={batch.status} /> },
                { label: 'Payment date', value: formatDate(batch.paymentDate) },
                { label: 'Payment method', value: enumLabel(batch.paymentMethod) },
                { label: 'Total employees', value: batch.totalEmployees },
                { label: 'Total amount', value: <span className="font-semibold tabular-nums">{formatAmount(batch.totalAmount)}</span> },
                { label: 'Paid so far', value: <span className="tabular-nums">{formatAmount(batch.paidAmount)}</span> },
                { label: 'Created', value: `${formatDateTime(batch.createdAt)}${batch.createdBy ? ` · ${batch.createdBy}` : ''}` },
              ]}
            />
            <div className="flex flex-wrap gap-2 text-sm">
              <span className="badge badge-outline">{counts.pending} pending</span>
              <span className="badge badge-soft badge-info">{counts.processing} processing</span>
              <span className="badge badge-soft badge-success">{counts.paid} paid</span>
              <span className="badge badge-soft badge-error">{counts.failed} failed</span>
              {counts.cancelled > 0 && <span className="badge badge-outline badge-error">{counts.cancelled} cancelled</span>}
            </div>
            {batch.notes && <p className="whitespace-pre-line text-sm text-base-content/70">{batch.notes}</p>}
          </div>
        </div>

        {/* Reloads its rows whenever the batch changes (e.g. processing started) so they match the summary. */}
        <PaymentList batchId={batch.id} refreshKey={batch.updatedAt ?? batch.createdAt} onChanged={reload} />
      </div>
      {dialogs}
    </>
  )
}
