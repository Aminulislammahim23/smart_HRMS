import { Landmark, Plus } from 'lucide-react'
import { useCallback, useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { PayrollPeriodTable } from '../../components/payroll/PayrollPeriodTable'
import { useApi } from '../../hooks/useApi'
import { usePagination } from '../../hooks/usePagination'
import { getPayrollPeriods } from '../../services/payrollService'
import { PAYROLL_STATUSES, isPayrollStatus } from '../../types/payroll'
import { enumLabel } from '../../utils/formatters'

const CURRENT_YEAR = new Date().getFullYear()

/** Every payroll period, filtered by year and status on the server. Filters are kept in the URL. */
export default function PayrollPeriodsPage() {
  const [params, setParams] = useSearchParams()
  const yearParam = Number(params.get('year'))
  const year = Number.isInteger(yearParam) && yearParam >= 2000 && yearParam <= 2100 ? yearParam : undefined
  const statusParam = params.get('status') ?? ''
  const status = isPayrollStatus(statusParam) ? statusParam : undefined

  const setFilter = (key: 'year' | 'status', value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  const load = useCallback((signal: AbortSignal) => getPayrollPeriods({ year, status }, signal), [year, status])
  const { data: periods, error, loading, reload } = useApi(load)
  const rows = useMemo(() => periods ?? [], [periods])
  const pagination = usePagination(rows, 10)

  return (
    <>
      <PageHeader
        title="Payroll periods"
        description="Create, calculate, review and approve monthly payroll."
        actions={
          <Link to="/payroll/create" className="btn btn-primary btn-sm">
            <Plus className="size-4" /> New payroll period
          </Link>
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="flex flex-col gap-3 sm:flex-row">
            <select className="select select-sm w-full sm:w-36" value={year ?? ''} onChange={(e) => setFilter('year', e.target.value)} aria-label="Year">
              <option value="">All years</option>
              {Array.from({ length: 6 }, (_, i) => CURRENT_YEAR + 1 - i).map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
            <select className="select select-sm w-full sm:w-48" value={status ?? ''} onChange={(e) => setFilter('status', e.target.value)} aria-label="Status">
              <option value="">All statuses</option>
              {PAYROLL_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {enumLabel(s)}
                </option>
              ))}
            </select>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !periods ? (
            <Loading label="Loading payroll periods…" />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Landmark}
              title={year || status ? 'No payroll periods match these filters' : 'No payroll periods yet'}
              description="A payroll period is usually one calendar month."
              action={
                <Link to="/payroll/create" className="btn btn-primary btn-sm">
                  <Plus className="size-4" /> New payroll period
                </Link>
              }
            />
          ) : (
            <>
              <PayrollPeriodTable periods={pagination.pageItems} />
              <Pagination
                page={pagination.page}
                pageCount={pagination.pageCount}
                pageSize={pagination.pageSize}
                total={pagination.total}
                onPageChange={pagination.setPage}
                onPageSizeChange={pagination.setPageSize}
              />
            </>
          )}
        </div>
      </div>
    </>
  )
}
