import { Eye, History, Printer, ReceiptText, RotateCcw, Search } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { PayrollRecordModal } from '../../components/payroll/PayrollRecordModal'
import { PaymentStatusBadge, PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { useApi } from '../../hooks/useApi'
import { getDepartments } from '../../services/departmentService'
import { getEmployees } from '../../services/employeeService'
import { getPayrollHistory, getPayslips } from '../../services/payrollService'
import {
  HISTORY_SORTS,
  PAYMENT_STATUSES,
  PAYROLL_STATUSES,
  isPaymentStatus,
  isPayrollStatus,
  type HistorySort,
  type PayrollHistoryQuery,
  type PayrollRecord,
} from '../../types/payroll'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'
import { enumLabel, formatAmount, formatDate } from '../../utils/formatters'

const CURRENT_YEAR = new Date().getFullYear()
const MONTHS = Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: new Date(2000, i, 1).toLocaleDateString(undefined, { month: 'long' }) }))
const PAGE_SIZES: readonly number[] = PAGE_SIZE_OPTIONS
const SORT_LABELS: Record<HistorySort, string> = { period: 'Pay period', employee: 'Employee', gross: 'Gross salary', net: 'Net salary', paymentDate: 'Payment date' }

function readInt(params: URLSearchParams, key: string, min: number, max: number): number | undefined {
  const value = Number(params.get(key))
  return Number.isInteger(value) && value >= min && value <= max ? value : undefined
}

/** Reads the filters from the URL; anything invalid is ignored (the server validates again). */
function queryFromParams(params: URLSearchParams): PayrollHistoryQuery {
  const status = params.get('status') ?? ''
  const paymentStatus = params.get('paymentStatus') ?? ''
  const sortBy = params.get('sortBy') ?? ''
  const direction = params.get('sortDirection')
  return {
    employeeId: params.get('employeeId') || undefined,
    departmentId: params.get('departmentId') || undefined,
    month: readInt(params, 'month', 1, 12),
    year: readInt(params, 'year', 2000, 2100),
    status: isPayrollStatus(status) ? status : undefined,
    paymentStatus: isPaymentStatus(paymentStatus) ? paymentStatus : undefined,
    search: params.get('search') || undefined,
    page: readInt(params, 'page', 1, 100_000) ?? 1,
    pageSize: PAGE_SIZES.includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 25,
    sortBy: HISTORY_SORTS.some((s) => s === sortBy) ? (sortBy as HistorySort) : undefined,
    sortDirection: direction === 'asc' || direction === 'desc' ? direction : undefined,
  }
}

/**
 * HR/Admin payroll history (every record, any status) or payslip list (issued payslips only). Filtering, sorting
 * and paging happen on the server; the filters live in the URL so a view can be bookmarked.
 */
export default function PayrollHistoryPage({ mode = 'history' }: { mode?: 'history' | 'payslips' }) {
  const [params, setParams] = useSearchParams()
  const query = useMemo(() => queryFromParams(params), [params])
  const [searchInput, setSearchInput] = useState(query.search ?? '')
  const [viewing, setViewing] = useState<PayrollRecord | null>(null)

  const update = useCallback(
    (patch: Record<string, string | number | undefined>, resetPage = true) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          for (const [key, value] of Object.entries(patch)) {
            if (value === undefined || value === '') next.delete(key)
            else next.set(key, String(value))
          }
          if (resetPage && !('page' in patch)) next.delete('page')
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

  const lookups = useCallback((signal: AbortSignal) => Promise.all([getEmployees(signal), getDepartments(signal)]), [])
  const { data: lookupData } = useApi(lookups)
  const [employees, departments] = lookupData ?? [[], []]

  const load = useCallback((signal: AbortSignal) => (mode === 'history' ? getPayrollHistory(query, signal) : getPayslips(query, signal)), [mode, query])
  const { data, error, loading, reload } = useApi(load)
  const filtered = !!(query.employeeId || query.departmentId || query.month || query.year || query.status || query.paymentStatus || query.search)

  const reset = () => {
    setSearchInput('')
    setParams(new URLSearchParams(), { replace: true })
  }

  const columns: Column<PayrollRecord>[] = [
    {
      key: 'employee',
      header: 'Employee',
      render: (r) => (
        <div className="min-w-0">
          <p className="truncate font-medium">{r.employeeName}</p>
          <p className="text-xs text-base-content/60">{r.employeeCode}</p>
        </div>
      ),
    },
    { key: 'department', header: 'Department', render: (r) => r.departmentName ?? '—', className: 'hidden 2xl:table-cell' },
    {
      key: 'period',
      header: 'Pay period',
      render: (r) => (
        <div>
          <p className="whitespace-nowrap">{r.periodName}</p>
          {r.payslipNumber && <p className="text-xs text-base-content/60">{r.payslipNumber}</p>}
        </div>
      ),
    },
    { key: 'gross', header: 'Gross', render: (r) => <span className="tabular-nums">{formatAmount(r.grossSalary)}</span>, className: 'hidden lg:table-cell text-right' },
    { key: 'deduction', header: 'Deduction', render: (r) => <span className="tabular-nums">{formatAmount(r.totalDeduction)}</span>, className: 'hidden xl:table-cell text-right' },
    { key: 'net', header: 'Net', render: (r) => <span className="font-semibold tabular-nums">{formatAmount(r.netSalary)}</span>, className: 'text-right' },
    { key: 'status', header: 'Payroll', render: (r) => <PayrollStatusBadge status={r.periodStatus} />, className: 'hidden md:table-cell' },
    { key: 'payment', header: 'Payment', render: (r) => <PaymentStatusBadge status={r.paymentStatus} /> },
    { key: 'paymentDate', header: 'Paid on', render: (r) => (r.paymentDate ? formatDate(r.paymentDate) : '—'), className: 'hidden lg:table-cell whitespace-nowrap' },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (r) => (
        <div className="flex justify-end gap-1">
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setViewing(r)} title="View" aria-label={`View payroll of ${r.employeeName}, ${r.periodName}`}>
            <Eye className="size-4" />
          </button>
          {r.payslipId ? (
            <>
              <Link to={`/payroll/payslips/${r.payslipId}`} className="btn btn-ghost btn-xs btn-square" title="Payslip" aria-label={`Payslip of ${r.employeeName}, ${r.periodName}`}>
                <ReceiptText className="size-4" />
              </Link>
              <Link to={`/payroll/payslips/${r.payslipId}?print=1`} className="btn btn-ghost btn-xs btn-square" title="Print" aria-label={`Print payslip of ${r.employeeName}, ${r.periodName}`}>
                <Printer className="size-4" />
              </Link>
            </>
          ) : (
            <span className="btn btn-ghost btn-xs btn-square btn-disabled" title="No payslip until the payroll is approved">
              <ReceiptText className="size-4 opacity-40" />
            </span>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title={mode === 'history' ? 'Payroll history' : 'Payslips'}
        description={
          mode === 'history'
            ? 'Every payroll record with its payslip and payment status.'
            : 'Issued payslips. A payslip is issued when its payroll is approved.'
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <label className="input input-sm w-full">
              <Search className="size-4 opacity-50" />
              <input type="search" placeholder="Search name or employee ID" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} aria-label="Search" />
            </label>
            <select className="select select-sm w-full" value={query.employeeId ?? ''} onChange={(e) => update({ employeeId: e.target.value })} aria-label="Employee">
              <option value="">All employees</option>
              {employees.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName} ({e.employeeCode})
                </option>
              ))}
            </select>
            <select className="select select-sm w-full" value={query.departmentId ?? ''} onChange={(e) => update({ departmentId: e.target.value })} aria-label="Department">
              <option value="">All departments</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
            <div className="grid grid-cols-2 gap-2">
              <select className="select select-sm w-full" value={query.month ?? ''} onChange={(e) => update({ month: e.target.value })} aria-label="Month">
                <option value="">Any month</option>
                {MONTHS.map((m) => (
                  <option key={m.value} value={m.value}>
                    {m.label}
                  </option>
                ))}
              </select>
              <select className="select select-sm w-full" value={query.year ?? ''} onChange={(e) => update({ year: e.target.value })} aria-label="Year">
                <option value="">Any year</option>
                {Array.from({ length: 6 }, (_, i) => CURRENT_YEAR + 1 - i).map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </select>
            </div>
            <select className="select select-sm w-full" value={query.status ?? ''} onChange={(e) => update({ status: e.target.value })} aria-label="Payroll status">
              <option value="">Any payroll status</option>
              {(mode === 'history' ? PAYROLL_STATUSES : (['Approved', 'Finalized', 'Paid'] as const)).map((s) => (
                <option key={s} value={s}>
                  {enumLabel(s)}
                </option>
              ))}
            </select>
            <select className="select select-sm w-full" value={query.paymentStatus ?? ''} onChange={(e) => update({ paymentStatus: e.target.value })} aria-label="Payment status">
              <option value="">Any payment status</option>
              {PAYMENT_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
            <div className="grid grid-cols-2 gap-2">
              <select className="select select-sm w-full" value={query.sortBy ?? 'period'} onChange={(e) => update({ sortBy: e.target.value === 'period' ? undefined : e.target.value })} aria-label="Sort by">
                {HISTORY_SORTS.map((s) => (
                  <option key={s} value={s}>
                    {SORT_LABELS[s]}
                  </option>
                ))}
              </select>
              <select className="select select-sm w-full" value={query.sortDirection ?? ''} onChange={(e) => update({ sortDirection: e.target.value })} aria-label="Sort direction">
                <option value="">Default order</option>
                <option value="asc">Ascending</option>
                <option value="desc">Descending</option>
              </select>
            </div>
            <button type="button" className="btn btn-sm btn-ghost justify-self-start" onClick={reset} disabled={!filtered && !query.sortBy && !query.sortDirection}>
              <RotateCcw className="size-4" /> Reset filters
            </button>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label={mode === 'history' ? 'Loading payroll history…' : 'Loading payslips…'} />
          ) : !data || data.items.length === 0 ? (
            <EmptyState
              icon={History}
              title={filtered ? 'Nothing matches these filters' : mode === 'history' ? 'No payroll yet' : 'No payslips issued yet'}
              description={filtered ? 'Try other filters or reset them.' : 'Payslips are issued when a payroll period is approved.'}
              action={filtered && <button type="button" className="btn btn-sm" onClick={reset}>Reset filters</button>}
            />
          ) : (
            <>
              <p className="text-sm text-base-content/60">
                {data.totalCount} {mode === 'history' ? 'records' : 'payslips'}
                {loading && <span className="loading loading-spinner loading-xs ml-2 align-middle" />}
              </p>
              <Table columns={columns} rows={data.items} rowKey={(r) => r.id} compact />
              <Pagination
                page={data.page}
                pageCount={Math.max(1, data.totalPages)}
                pageSize={data.pageSize}
                total={data.totalCount}
                onPageChange={(page) => update({ page }, false)}
                onPageSizeChange={(pageSize) => update({ pageSize, page: undefined })}
              />
            </>
          )}
        </div>
      </div>

      {viewing && <PayrollRecordModal record={viewing} onClose={() => setViewing(null)} />}
    </>
  )
}
