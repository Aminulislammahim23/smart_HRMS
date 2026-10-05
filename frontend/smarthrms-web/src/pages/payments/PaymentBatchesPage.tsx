import { Ban, Eye, Play, RotateCcw, Wallet } from 'lucide-react'
import { useCallback, useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { PaymentBatchStatusBadge } from '../../components/payments/PaymentBadges'
import { useBatchActions } from '../../components/payments/useBatchActions'
import { useApi } from '../../hooks/useApi'
import { getPaymentBatches } from '../../services/paymentService'
import { getPayrollPeriods } from '../../services/payrollService'
import { PAYMENT_BATCH_STATUSES, isPaymentBatchStatus, type PaymentBatch, type PaymentBatchQuery } from '../../types/payment'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'
import { enumLabel, formatAmount, formatDate, formatDateTime } from '../../utils/formatters'

const PAGE_SIZES: readonly number[] = PAGE_SIZE_OPTIONS

function queryFromParams(params: URLSearchParams): PaymentBatchQuery {
  const status = params.get('status') ?? ''
  const page = Number(params.get('page'))
  return {
    payrollPeriodId: params.get('payrollPeriodId') || undefined,
    status: isPaymentBatchStatus(status) ? status : undefined,
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    pageSize: PAGE_SIZES.includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 25,
  }
}

/**
 * Salary payment batches (HR/Admin view; Admin acts). A batch is created from a finalized payroll on its period page.
 * Process and Cancel appear only when the server allows them.
 */
export default function PaymentBatchesPage() {
  const [params, setParams] = useSearchParams()
  const query = useMemo(() => queryFromParams(params), [params])

  const update = (patch: Record<string, string | number | undefined>) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        for (const [key, value] of Object.entries(patch)) {
          if (value === undefined || value === '') next.delete(key)
          else next.set(key, String(value))
        }
        if (!('page' in patch)) next.delete('page')
        return next
      },
      { replace: true },
    )

  const periodsLoad = useCallback((signal: AbortSignal) => getPayrollPeriods({}, signal), [])
  const { data: periods } = useApi(periodsLoad)
  const load = useCallback((signal: AbortSignal) => getPaymentBatches(query, signal), [query])
  const { data, error, loading, reload } = useApi(load)
  const { request, dialogs } = useBatchActions(() => reload())
  const filtered = !!(query.payrollPeriodId || query.status)

  const columns: Column<PaymentBatch>[] = [
    {
      key: 'number',
      header: 'Batch number',
      render: (b) => (
        <Link to={`/payments/batches/${b.id}`} className="font-medium whitespace-nowrap hover:text-primary">
          {b.batchNumber}
        </Link>
      ),
    },
    {
      key: 'period',
      header: 'Payroll period',
      render: (b) => (
        <Link to={`/payroll/${b.payrollPeriodId}`} className="whitespace-nowrap hover:text-primary">
          {b.periodName}
        </Link>
      ),
    },
    { key: 'date', header: 'Payment date', render: (b) => formatDate(b.paymentDate), className: 'hidden md:table-cell whitespace-nowrap' },
    { key: 'method', header: 'Method', render: (b) => enumLabel(b.paymentMethod), className: 'hidden lg:table-cell whitespace-nowrap' },
    { key: 'employees', header: 'Employees', render: (b) => b.totalEmployees, className: 'hidden sm:table-cell text-right' },
    { key: 'amount', header: 'Total amount', render: (b) => <span className="font-semibold tabular-nums">{formatAmount(b.totalAmount)}</span>, className: 'text-right' },
    { key: 'status', header: 'Status', render: (b) => <PaymentBatchStatusBadge status={b.status} /> },
    { key: 'createdBy', header: 'Created by', render: (b) => b.createdBy ?? '—', className: 'hidden xl:table-cell whitespace-nowrap' },
    { key: 'created', header: 'Created', render: (b) => formatDateTime(b.createdAt), className: 'hidden 2xl:table-cell whitespace-nowrap' },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (b) => (
        <div className="flex justify-end gap-1">
          <Link to={`/payments/batches/${b.id}`} className="btn btn-ghost btn-xs btn-square" title="View" aria-label={`View ${b.batchNumber}`}>
            <Eye className="size-4" />
          </Link>
          {b.actions.canProcess && (
            <button type="button" className="btn btn-ghost btn-xs btn-square text-primary" onClick={() => request('process', b)} title="Process" aria-label={`Process ${b.batchNumber}`}>
              <Play className="size-4" />
            </button>
          )}
          {b.actions.canCancel && (
            <button type="button" className="btn btn-ghost btn-xs btn-square text-error" onClick={() => request('cancel', b)} title="Cancel batch" aria-label={`Cancel ${b.batchNumber}`}>
              <Ban className="size-4" />
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader title="Payment batches" description="Salary payments of finalized payroll, one batch per payment run." />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="grid gap-3 sm:grid-cols-3">
            <select className="select select-sm w-full" value={query.payrollPeriodId ?? ''} onChange={(e) => update({ payrollPeriodId: e.target.value })} aria-label="Payroll period">
              <option value="">All payroll periods</option>
              {(periods ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
            <select className="select select-sm w-full" value={query.status ?? ''} onChange={(e) => update({ status: e.target.value })} aria-label="Batch status">
              <option value="">Any status</option>
              {PAYMENT_BATCH_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {enumLabel(s)}
                </option>
              ))}
            </select>
            <button type="button" className="btn btn-sm btn-ghost justify-self-start" onClick={() => setParams(new URLSearchParams(), { replace: true })} disabled={!filtered}>
              <RotateCcw className="size-4" /> Reset filters
            </button>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label="Loading payment batches…" />
          ) : !data || data.items.length === 0 ? (
            <EmptyState
              icon={Wallet}
              title={filtered ? 'Nothing matches these filters' : 'No payment batches yet'}
              description={filtered ? 'Try other filters or reset them.' : 'Finalize an approved payroll, then create a payment batch from its period page.'}
              action={!filtered && <Link to="/payroll/periods" className="btn btn-sm">Payroll periods</Link>}
            />
          ) : (
            <>
              <p className="text-sm text-base-content/60">
                {data.totalCount} batches
                {loading && <span className="loading loading-spinner loading-xs ml-2 align-middle" />}
              </p>
              <Table columns={columns} rows={data.items} rowKey={(b) => b.id} compact />
              <Pagination
                page={data.page}
                pageCount={Math.max(1, data.totalPages)}
                pageSize={data.pageSize}
                total={data.totalCount}
                onPageChange={(page) => update({ page })}
                onPageSizeChange={(pageSize) => update({ pageSize, page: undefined })}
              />
            </>
          )}
        </div>
      </div>
      {dialogs}
    </>
  )
}
