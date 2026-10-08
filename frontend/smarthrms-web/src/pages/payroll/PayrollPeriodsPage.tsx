import { Eye, Landmark, ListChecks, Plus, RotateCcw, Search } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { ExportButtons } from '../../components/common/ExportButtons'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { LockedBadge, PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { useApi } from '../../hooks/useApi'
import { exportPayrollReport, getPayrollPeriodHistory } from '../../services/payrollReportService'
import { PAYROLL_STATUSES, isPayrollStatus } from '../../types/payroll'
import type { PayrollPeriodHistory, PayrollPeriodHistoryQuery } from '../../types/payrollReport'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'
import { enumLabel, formatAmount, formatDate, formatDateTime, formatInstantDate } from '../../utils/formatters'

const CURRENT_YEAR = new Date().getFullYear()
const MONTHS = Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: new Date(2000, i, 1).toLocaleDateString(undefined, { month: 'long' }) }))
const PAGE_SIZES: readonly number[] = PAGE_SIZE_OPTIONS
const DATE = /^\d{4}-\d{2}-\d{2}$/

function readInt(params: URLSearchParams, key: string, min: number, max: number): number | undefined {
  const value = Number(params.get(key))
  return Number.isInteger(value) && value >= min && value <= max ? value : undefined
}

/** Reads the filters from the URL; anything invalid is ignored (the server validates again). */
function queryFromParams(params: URLSearchParams): PayrollPeriodHistoryQuery {
  const status = params.get('status') ?? ''
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''
  return {
    search: params.get('search') || undefined,
    month: readInt(params, 'month', 1, 12),
    year: readInt(params, 'year', 2000, 2100),
    status: isPayrollStatus(status) ? status : undefined,
    from: DATE.test(from) ? from : undefined,
    to: DATE.test(to) ? to : undefined,
    page: readInt(params, 'page', 1, 100_000) ?? 1,
    pageSize: PAGE_SIZES.includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 10,
  }
}

const amount = (value: number, strong = false) => <span className={`tabular-nums ${strong ? 'font-semibold' : ''}`}>{formatAmount(value)}</span>

/**
 * Payroll history by period (Day 19): every payroll period with its totals, processed and finalized dates. Search,
 * month, year, status and date range are filtered and paged on the server; the filters live in the URL.
 */
export default function PayrollPeriodsPage() {
  const [params, setParams] = useSearchParams()
  const query = useMemo(() => queryFromParams(params), [params])
  const [searchInput, setSearchInput] = useState(query.search ?? '')

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

  const load = useCallback((signal: AbortSignal) => getPayrollPeriodHistory(query, signal), [query])
  const { data, error, loading, reload } = useApi(load)
  const filtered = !!(query.search || query.month || query.year || query.status || query.from || query.to)

  const reset = () => {
    setSearchInput('')
    setParams(new URLSearchParams(), { replace: true })
  }

  const columns: Column<PayrollPeriodHistory>[] = [
    {
      key: 'period',
      header: 'Payroll period',
      render: (p) => (
        <div>
          <Link to={`/payroll/${p.id}`} className="font-medium whitespace-nowrap hover:text-primary">
            {p.name}
          </Link>
          <p className="text-xs whitespace-nowrap text-base-content/60">
            {formatDate(p.startDate)} – {formatDate(p.endDate)}
          </p>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (p) => (
        <div className="flex flex-wrap gap-1">
          <PayrollStatusBadge status={p.status} />
          {p.isLocked && <LockedBadge />}
        </div>
      ),
    },
    { key: 'employees', header: 'Employees', render: (p) => p.employeeCount, className: 'hidden sm:table-cell text-right' },
    { key: 'gross', header: 'Gross', render: (p) => amount(p.grossSalary), className: 'hidden md:table-cell text-right' },
    { key: 'allowances', header: 'Allowances', render: (p) => amount(p.totalAllowances), className: 'hidden 2xl:table-cell text-right' },
    { key: 'overtime', header: 'Overtime', render: (p) => amount(p.overtime), className: 'hidden 2xl:table-cell text-right' },
    { key: 'bonus', header: 'Bonus', render: (p) => amount(p.bonus), className: 'hidden 2xl:table-cell text-right' },
    { key: 'tax', header: 'Tax', render: (p) => amount(p.tax), className: 'hidden xl:table-cell text-right' },
    { key: 'deductions', header: 'Deductions', render: (p) => amount(p.totalDeduction), className: 'hidden lg:table-cell text-right' },
    { key: 'net', header: 'Net payroll', render: (p) => amount(p.netSalary, true), className: 'text-right' },
    {
      key: 'dates',
      header: 'Processed / Finalized',
      render: (p) => (
        <div className="whitespace-nowrap text-xs">
          <p title={p.calculatedAt ? formatDateTime(p.calculatedAt) : undefined}>{formatInstantDate(p.calculatedAt)}</p>
          <p className="text-base-content/60" title={p.finalizedAt ? formatDateTime(p.finalizedAt) : undefined}>
            {p.finalizedAt ? formatInstantDate(p.finalizedAt) : 'Not finalized'}
          </p>
        </div>
      ),
      className: 'hidden xl:table-cell',
    },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (p) => (
        <div className="flex justify-end gap-1">
          <Link to={`/payroll/${p.id}`} className="btn btn-ghost btn-xs btn-square" title="View details" aria-label={`View ${p.name}`}>
            <Eye className="size-4" />
          </Link>
          <Link to={`/payroll/${p.id}/records`} className="btn btn-ghost btn-xs btn-square" title="Employee records" aria-label={`Records of ${p.name}`}>
            <ListChecks className="size-4" />
          </Link>
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Payroll periods"
        description="Payroll history by period: totals, status, processed and finalized dates."
        actions={
          <>
            <ExportButtons label="Export payroll history" onExport={(format) => exportPayrollReport('periods', { ...query, page: undefined, pageSize: undefined }, format)} />
            <Link to="/payroll/create" className="btn btn-primary btn-sm">
              <Plus className="size-4" /> New payroll period
            </Link>
          </>
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <label className="input input-sm w-full">
              <Search className="size-4 opacity-50" />
              <input type="search" placeholder="Search period name" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} aria-label="Search" />
            </label>
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
            <select className="select select-sm w-full" value={query.status ?? ''} onChange={(e) => update({ status: e.target.value })} aria-label="Status">
              <option value="">All statuses</option>
              {PAYROLL_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {enumLabel(s)}
                </option>
              ))}
            </select>
            <div className="grid grid-cols-2 gap-2">
              <input type="date" className="input input-sm w-full" value={query.from ?? ''} max={query.to} onChange={(e) => update({ from: e.target.value })} aria-label="From date" title="Periods overlapping from" />
              <input type="date" className="input input-sm w-full" value={query.to ?? ''} min={query.from} onChange={(e) => update({ to: e.target.value })} aria-label="To date" title="Periods overlapping until" />
            </div>
            <button type="button" className="btn btn-sm btn-ghost justify-self-start" onClick={reset} disabled={!filtered}>
              <RotateCcw className="size-4" /> Reset filters
            </button>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label="Loading payroll periods…" />
          ) : !data || data.items.length === 0 ? (
            <EmptyState
              icon={Landmark}
              title={filtered ? 'No payroll periods match these filters' : 'No payroll periods yet'}
              description={filtered ? 'Try other filters or reset them.' : 'A payroll period is usually one calendar month.'}
              action={
                filtered ? (
                  <button type="button" className="btn btn-sm" onClick={reset}>Reset filters</button>
                ) : (
                  <Link to="/payroll/create" className="btn btn-primary btn-sm">
                    <Plus className="size-4" /> New payroll period
                  </Link>
                )
              }
            />
          ) : (
            <>
              <p className="text-sm text-base-content/60">
                {data.totalCount} payroll periods
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
      </div>
    </>
  )
}
