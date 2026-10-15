import { AlertTriangle, BadgeCheck, Banknote, Hourglass, Landmark, ListChecks, Plus, RotateCcw, Wallet, type LucideIcon } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { CreatePaymentBatchModal } from '../../components/payments/CreatePaymentBatchModal'
import { useApi } from '../../hooks/useApi'
import { getDisbursementSummary } from '../../services/paymentService'
import type { DisbursementFigures, DisbursementSummaryQuery, PayrollDisbursement } from '../../types/payment'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'
import { formatAmount, formatDate } from '../../utils/formatters'

const PAGE_SIZES: readonly number[] = PAGE_SIZE_OPTIONS
const THIS_YEAR = new Date().getFullYear()
const YEARS = Array.from({ length: 6 }, (_, i) => THIS_YEAR - i)

function queryFromParams(params: URLSearchParams): DisbursementSummaryQuery {
  const year = Number(params.get('year'))
  const page = Number(params.get('page'))
  const pageSize = Number(params.get('pageSize'))
  return {
    year: YEARS.includes(year) ? year : undefined,
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    pageSize: PAGE_SIZES.includes(pageSize) ? pageSize : 10,
  }
}

interface Card {
  label: string
  value: string
  hint: string
  icon: LucideIcon
  tone: string
}

/** The headline figures. Every number is the server's (no client-side maths besides formatting). */
function SummaryCards({ totals }: { totals: DisbursementFigures }) {
  const cards: Card[] = [
    { label: 'Finalized payable', value: formatAmount(totals.payableAmount), hint: `${totals.payableCount} employee payments`, icon: Landmark, tone: 'bg-primary/10 text-primary' },
    { label: 'Paid', value: formatAmount(totals.paidAmount), hint: `${totals.paidCount} successful payments`, icon: BadgeCheck, tone: 'bg-success/10 text-success' },
    { label: 'Pending (outstanding)', value: formatAmount(totals.outstandingAmount), hint: `${totals.notInitiatedCount} not started · ${totals.pendingCount} queued`, icon: Hourglass, tone: 'bg-warning/10 text-warning' },
    { label: 'Processing', value: String(totals.processingCount), hint: formatAmount(totals.processingAmount), icon: Banknote, tone: 'bg-info/10 text-info' },
    { label: 'Failed', value: String(totals.failedCount), hint: formatAmount(totals.failedAmount), icon: AlertTriangle, tone: 'bg-error/10 text-error' },
    { label: 'Successful', value: String(totals.paidCount), hint: 'Payments confirmed paid', icon: ListChecks, tone: 'bg-success/10 text-success' },
  ]

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-6">
      {cards.map(({ label, value, hint, icon: Icon, tone }) => (
        <div key={label} className="card bg-base-100 shadow-sm">
          <div className="card-body flex-row items-center gap-4 p-5">
            <div className={`rounded-xl p-3 ${tone}`}>
              <Icon className="size-6" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-base-content/60">{label}</p>
              <p className="truncate text-xl font-semibold tabular-nums">{value}</p>
              <p className="truncate text-xs text-base-content/50">{hint}</p>
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

function StatusCounts({ row }: { row: PayrollDisbursement }) {
  const parts: { label: string; count: number; className: string; status?: string }[] = [
    { label: 'not started', count: row.notInitiatedCount, className: 'badge-outline' },
    { label: 'pending', count: row.pendingCount, className: 'badge-outline', status: 'Pending' },
    { label: 'processing', count: row.processingCount, className: 'badge-soft badge-info', status: 'Processing' },
    { label: 'failed', count: row.failedCount, className: 'badge-soft badge-error', status: 'Failed' },
  ]
  const shown = parts.filter((p) => p.count > 0)
  if (shown.length === 0) return <span className="badge badge-sm badge-soft badge-success whitespace-nowrap">All paid</span>

  return (
    <div className="flex flex-wrap gap-1">
      {shown.map((p) =>
        p.status ? (
          <Link key={p.label} to={`/payments/history?payrollPeriodId=${row.payrollPeriodId}&status=${p.status}`} className={`badge badge-sm whitespace-nowrap ${p.className}`}>
            {p.count} {p.label}
          </Link>
        ) : (
          <span key={p.label} className={`badge badge-sm whitespace-nowrap ${p.className}`}>
            {p.count} {p.label}
          </span>
        ),
      )}
    </div>
  )
}

/**
 * Payroll disbursement dashboard (HR/Admin): what finalized payroll owes, what is paid, and what is still pending,
 * processing or failed, per payroll. Payments are recorded, not executed: there is no bank integration. Creating a
 * batch is offered only where the server allows it (Admin, finalized payroll, no open batch, someone left to pay).
 */
export default function DisbursementDashboardPage() {
  const [params, setParams] = useSearchParams()
  const query = useMemo(() => queryFromParams(params), [params])
  const [creating, setCreating] = useState<PayrollDisbursement | null>(null)

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

  const load = useCallback((signal: AbortSignal) => getDisbursementSummary(query, signal), [query])
  const { data, error, loading, reload } = useApi(load)

  const columns: Column<PayrollDisbursement>[] = [
    {
      key: 'period',
      header: 'Payroll period',
      render: (r) => (
        <div>
          <Link to={`/payroll/${r.payrollPeriodId}`} className="font-medium whitespace-nowrap hover:text-primary">
            {r.periodName}
          </Link>
          <p className="text-xs whitespace-nowrap text-base-content/60">
            {formatDate(r.startDate)} – {formatDate(r.endDate)}
          </p>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Payroll',
      render: (r) => <span className={`badge badge-sm whitespace-nowrap ${r.payrollStatus === 'Paid' ? 'badge-soft badge-success' : 'badge-soft badge-primary'}`}>{r.payrollStatus}</span>,
      className: 'hidden md:table-cell',
    },
    { key: 'payable', header: 'Payable', render: (r) => <span className="tabular-nums">{formatAmount(r.payableAmount)}</span>, className: 'text-right' },
    { key: 'paid', header: 'Paid', render: (r) => <span className="tabular-nums text-success">{formatAmount(r.paidAmount)}</span>, className: 'hidden sm:table-cell text-right' },
    { key: 'outstanding', header: 'Outstanding', render: (r) => <span className="font-semibold tabular-nums">{formatAmount(r.outstandingAmount)}</span>, className: 'text-right' },
    {
      key: 'progress',
      header: 'Paid employees',
      render: (r) => (
        <div className="min-w-28">
          <progress className="progress progress-success w-full" value={r.paidCount} max={Math.max(1, r.payableCount)} aria-label={`${r.paidCount} of ${r.payableCount} paid`} />
          <p className="text-xs text-base-content/60 tabular-nums">
            {r.paidCount} / {r.payableCount}
          </p>
        </div>
      ),
      className: 'hidden lg:table-cell',
    },
    { key: 'open', header: 'In progress', render: (r) => <StatusCounts row={r} />, className: 'hidden md:table-cell' },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (r) => (
        <div className="flex justify-end gap-1">
          <Link to={`/payments/history?payrollPeriodId=${r.payrollPeriodId}`} className="btn btn-ghost btn-xs" title="Payments of this payroll">
            Payments
          </Link>
          {r.canCreateBatch && (
            <button type="button" className="btn btn-primary btn-xs" onClick={() => setCreating(r)} title="Create a payment batch for the employees not yet in a payment">
              <Plus className="size-3" /> Batch
            </button>
          )}
        </div>
      ),
    },
  ]

  const rows = data?.payrolls.items ?? []

  return (
    <>
      <PageHeader
        title="Payroll disbursement"
        description="Finalized payroll, what has been paid and what is still pending, processing or failed. Payments are recorded here after the transfer; no bank is contacted."
        actions={
          <div className="flex flex-wrap gap-2">
            <Link to="/payments/batches" className="btn btn-sm">
              Payment batches
            </Link>
            <Link to="/payments/history?status=Failed" className="btn btn-sm">
              Failed payments
            </Link>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <select className="select select-sm w-40" value={query.year ?? ''} onChange={(e) => update({ year: e.target.value })} aria-label="Year">
          <option value="">All years</option>
          {YEARS.map((y) => (
            <option key={y} value={y}>
              {y}
            </option>
          ))}
        </select>
        <button type="button" className="btn btn-sm btn-ghost" onClick={() => setParams(new URLSearchParams(), { replace: true })} disabled={!query.year}>
          <RotateCcw className="size-4" /> Reset
        </button>
        {loading && data && <span className="loading loading-spinner loading-xs" />}
      </div>

      {error ? (
        <ErrorState error={error} onRetry={reload} />
      ) : loading && !data ? (
        <Loading label="Loading disbursement summary…" />
      ) : data ? (
        <>
          <SummaryCards totals={data.totals} />
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-4 p-4 sm:p-6">
              <h2 className="card-title text-base">Payroll to disburse</h2>
              {rows.length === 0 ? (
                <EmptyState
                  icon={Wallet}
                  title="No finalized payroll"
                  description="Only finalized payroll can be paid. Approve and finalize a payroll first."
                  action={
                    <Link to="/payroll/periods" className="btn btn-sm">
                      Payroll periods
                    </Link>
                  }
                />
              ) : (
                <>
                  <Table columns={columns} rows={rows} rowKey={(r) => r.payrollPeriodId} compact />
                  <Pagination
                    page={data.payrolls.page}
                    pageCount={Math.max(1, data.payrolls.totalPages)}
                    pageSize={data.payrolls.pageSize}
                    total={data.payrolls.totalCount}
                    onPageChange={(page) => update({ page })}
                    onPageSizeChange={(pageSize) => update({ pageSize, page: undefined })}
                  />
                </>
              )}
            </div>
          </div>
        </>
      ) : null}

      {creating && (
        <CreatePaymentBatchModal
          period={{ id: creating.payrollPeriodId, name: creating.periodName, startDate: creating.startDate }}
          unpaid={creating.notInitiatedCount}
          amount={creating.notInitiatedAmount}
          amountLabel="to pay"
          onClose={() => setCreating(null)}
        />
      )}
    </>
  )
}
