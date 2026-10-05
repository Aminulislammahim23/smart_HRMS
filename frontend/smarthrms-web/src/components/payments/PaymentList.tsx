import { Ban, CheckCircle2, Eye, RotateCcw, RotateCw, Search, Wallet, XCircle } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useApi } from '../../hooks/useApi'
import { getEmployees } from '../../services/employeeService'
import { getPayments } from '../../services/paymentService'
import { getPayrollPeriods } from '../../services/payrollService'
import {
  PAYMENT_METHODS,
  PAYMENT_TRANSACTION_STATUSES,
  isPaymentMethod,
  isPaymentTransactionStatus,
  type Payment,
  type PaymentQuery,
} from '../../types/payment'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'
import { enumLabel, formatAmount, formatDate, formatDateTime } from '../../utils/formatters'
import { EmptyState } from '../common/EmptyState'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { Pagination } from '../common/Pagination'
import { Table, type Column } from '../common/Table'
import { PaymentActionModal, type PaymentAction } from './PaymentActionModal'
import { PaymentTransactionStatusBadge } from './PaymentBadges'
import { PaymentDetailsModal } from './PaymentDetailsModal'

const PAGE_SIZES: readonly number[] = PAGE_SIZE_OPTIONS
const DATE = /^\d{4}-\d{2}-\d{2}$/

/** Reads the filters from the URL; anything invalid is ignored (the server validates again). */
function queryFromParams(params: URLSearchParams, batchId?: string): PaymentQuery {
  const status = params.get('status') ?? ''
  const method = params.get('paymentMethod') ?? ''
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''
  const page = Number(params.get('page'))
  return {
    batchId,
    employeeId: params.get('employeeId') || undefined,
    payrollPeriodId: batchId ? undefined : params.get('payrollPeriodId') || undefined,
    status: isPaymentTransactionStatus(status) ? status : undefined,
    paymentMethod: !batchId && isPaymentMethod(method) ? method : undefined,
    from: !batchId && DATE.test(from) ? from : undefined,
    to: !batchId && DATE.test(to) ? to : undefined,
    search: params.get('search') || undefined,
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    pageSize: PAGE_SIZES.includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 25,
  }
}

const ROW_ACTIONS: { action: PaymentAction; label: string; icon: typeof Wallet; className: string; allowed: (p: Payment) => boolean }[] = [
  { action: 'paid', label: 'Mark as paid', icon: CheckCircle2, className: 'text-success', allowed: (p) => p.actions.canMarkPaid },
  { action: 'failed', label: 'Mark as failed', icon: XCircle, className: 'text-error', allowed: (p) => p.actions.canMarkFailed },
  { action: 'retry', label: 'Retry', icon: RotateCw, className: '', allowed: (p) => p.actions.canRetry },
  { action: 'cancel', label: 'Cancel payment', icon: Ban, className: 'text-error', allowed: (p) => p.actions.canCancel },
]

/**
 * Salary payments with filters, paged on the server (HR/Admin). With `batchId` it lists one batch's payments; without
 * it, the payment history across batches. Row actions appear only when the server allows them (Admin, valid state,
 * not the Admin's own salary). `onChanged` runs after a status change, e.g. to refresh the batch summary; a new
 * `refreshKey` reloads the rows after a change made elsewhere on the page (filters and typing are kept).
 */
export function PaymentList({ batchId, refreshKey, onChanged }: { batchId?: string; refreshKey?: string; onChanged?: () => void }) {
  const [params, setParams] = useSearchParams()
  const query = useMemo(() => queryFromParams(params, batchId), [params, batchId])
  const [searchInput, setSearchInput] = useState(query.search ?? '')
  const [viewing, setViewing] = useState<string | null>(null)
  const [acting, setActing] = useState<{ payment: Payment; action: PaymentAction } | null>(null)

  const update = useCallback(
    (patch: Record<string, string | number | undefined>) =>
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
      ),
    [setParams],
  )

  // The search box is sent after a short pause in typing.
  useEffect(() => {
    if (searchInput.trim() === (query.search ?? '')) return
    const timer = window.setTimeout(() => update({ search: searchInput.trim() }), 350)
    return () => window.clearTimeout(timer)
  }, [searchInput, query.search, update])

  const lookups = useCallback((signal: AbortSignal) => Promise.all([getEmployees(signal), batchId ? Promise.resolve([]) : getPayrollPeriods({}, signal)]), [batchId])
  const { data: lookupData } = useApi(lookups)
  const [employees, periods] = lookupData ?? [[], []]

  const load = useCallback((signal: AbortSignal) => getPayments(query, signal), [query])
  const { data, error, loading, reload } = useApi(load)

  // A new refreshKey reloads in place (the rows stay on screen until the new ones arrive).
  const lastRefreshKey = useRef(refreshKey)
  useEffect(() => {
    if (lastRefreshKey.current === refreshKey) return
    lastRefreshKey.current = refreshKey
    reload()
  }, [refreshKey, reload])
  const filtered = !!(query.employeeId || query.payrollPeriodId || query.status || query.paymentMethod || query.from || query.to || query.search)

  const reset = () => {
    setSearchInput('')
    setParams(new URLSearchParams(), { replace: true })
  }

  const columns: Column<Payment>[] = [
    {
      key: 'employee',
      header: 'Employee',
      render: (p) => (
        <div className="min-w-0">
          <p className="truncate font-medium">{p.employeeName}</p>
          <p className="text-xs text-base-content/60">{p.employeeCode}</p>
        </div>
      ),
    },
    ...(batchId
      ? []
      : [
          {
            key: 'period',
            header: 'Payroll period',
            render: (p: Payment) => (
              <div>
                <p className="whitespace-nowrap">{p.periodName}</p>
                <p className="text-xs text-base-content/60">{p.batchNumber}</p>
              </div>
            ),
          },
        ]),
    { key: 'amount', header: 'Amount', render: (p) => <span className="font-semibold tabular-nums">{formatAmount(p.amount)}</span>, className: 'text-right' },
    ...(batchId ? [] : [{ key: 'method', header: 'Method', render: (p: Payment) => enumLabel(p.paymentMethod), className: 'hidden lg:table-cell whitespace-nowrap' }]),
    { key: 'status', header: 'Status', render: (p) => <PaymentTransactionStatusBadge status={p.status} /> },
    { key: 'reference', header: 'Transaction ref.', render: (p) => p.transactionReference ?? '—', className: 'hidden md:table-cell' },
    batchId
      ? { key: 'paidAt', header: 'Paid at', render: (p) => (p.status === 'Paid' ? formatDateTime(p.processedAt) : '—'), className: 'hidden lg:table-cell whitespace-nowrap' }
      : { key: 'date', header: 'Payment date', render: (p) => formatDate(p.paymentDate), className: 'hidden lg:table-cell whitespace-nowrap' },
    ...(batchId ? [{ key: 'failure', header: 'Failure reason', render: (p: Payment) => p.failureReason ?? '—', className: 'hidden xl:table-cell' }] : []),
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (p) => (
        <div className="flex justify-end gap-1">
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setViewing(p.id)} title="View" aria-label={`View payment of ${p.employeeName}`}>
            <Eye className="size-4" />
          </button>
          {ROW_ACTIONS.filter((a) => a.allowed(p)).map(({ action, label, icon: Icon, className }) => (
            <button
              key={action}
              type="button"
              className={`btn btn-ghost btn-xs btn-square ${className}`}
              onClick={() => setActing({ payment: p, action })}
              title={label}
              aria-label={`${label}: ${p.employeeName}`}
            >
              <Icon className="size-4" />
            </button>
          ))}
        </div>
      ),
    },
  ]

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4 p-4 sm:p-6">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label className="input input-sm w-full">
            <Search className="size-4 opacity-50" />
            <input type="search" placeholder="Search name, ID or reference" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} aria-label="Search" />
          </label>
          <select className="select select-sm w-full" value={query.employeeId ?? ''} onChange={(e) => update({ employeeId: e.target.value })} aria-label="Employee">
            <option value="">All employees</option>
            {employees.map((e) => (
              <option key={e.id} value={e.id}>
                {e.fullName} ({e.employeeCode})
              </option>
            ))}
          </select>
          <select className="select select-sm w-full" value={query.status ?? ''} onChange={(e) => update({ status: e.target.value })} aria-label="Payment status">
            <option value="">Any status</option>
            {PAYMENT_TRANSACTION_STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
          {!batchId && (
            <>
              <select className="select select-sm w-full" value={query.payrollPeriodId ?? ''} onChange={(e) => update({ payrollPeriodId: e.target.value })} aria-label="Payroll period">
                <option value="">All payroll periods</option>
                {periods.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
              <select className="select select-sm w-full" value={query.paymentMethod ?? ''} onChange={(e) => update({ paymentMethod: e.target.value })} aria-label="Payment method">
                <option value="">Any method</option>
                {PAYMENT_METHODS.map((m) => (
                  <option key={m} value={m}>
                    {enumLabel(m)}
                  </option>
                ))}
              </select>
              <div className="grid grid-cols-2 gap-2">
                <input type="date" className="input input-sm w-full" value={query.from ?? ''} max={query.to} onChange={(e) => update({ from: e.target.value })} aria-label="Paid from" title="Payment date from" />
                <input type="date" className="input input-sm w-full" value={query.to ?? ''} min={query.from} onChange={(e) => update({ to: e.target.value })} aria-label="Paid to" title="Payment date to" />
              </div>
            </>
          )}
          <button type="button" className="btn btn-sm btn-ghost justify-self-start" onClick={reset} disabled={!filtered}>
            <RotateCcw className="size-4" /> Reset filters
          </button>
        </div>

        {error ? (
          <ErrorState error={error} onRetry={reload} />
        ) : loading && !data ? (
          <Loading label="Loading payments…" />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            icon={Wallet}
            title={filtered ? 'Nothing matches these filters' : 'No payments yet'}
            description={filtered ? 'Try other filters or reset them.' : 'Payments are created with a payment batch for a finalized payroll.'}
            action={filtered && <button type="button" className="btn btn-sm" onClick={reset}>Reset filters</button>}
          />
        ) : (
          <>
            <p className="text-sm text-base-content/60">
              {data.totalCount} payments
              {loading && <span className="loading loading-spinner loading-xs ml-2 align-middle" />}
            </p>
            <Table columns={columns} rows={data.items} rowKey={(p) => p.id} compact />
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

      {viewing && <PaymentDetailsModal paymentId={viewing} onClose={() => setViewing(null)} />}
      {acting && (
        <PaymentActionModal
          payment={acting.payment}
          action={acting.action}
          onClose={() => setActing(null)}
          onDone={() => {
            setActing(null)
            reload()
            onChanged?.()
          }}
        />
      )}
    </div>
  )
}
