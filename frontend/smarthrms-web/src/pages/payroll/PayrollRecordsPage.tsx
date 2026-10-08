import { RotateCcw, Search, Users } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { ExportButtons } from '../../components/common/ExportButtons'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { PayrollRecordEditModal } from '../../components/payroll/PayrollRecordEditModal'
import { PayrollRecordModal } from '../../components/payroll/PayrollRecordModal'
import { PayrollRecordTable } from '../../components/payroll/PayrollRecordTable'
import { LockedBadge, PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { PayrollSummaryCards } from '../../components/payroll/PayrollSummaryCards'
import { useApi } from '../../hooks/useApi'
import { getDepartments } from '../../services/departmentService'
import { getDesignations } from '../../services/designationService'
import { getPayrollHistory, getPayrollPeriod } from '../../services/payrollService'
import { exportPayrollReport } from '../../services/payrollReportService'
import { PAYROLL_RECORD_STATUSES, isPayrollRecordStatus, type PayrollRecord } from '../../types/payroll'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'
import { enumLabel } from '../../utils/formatters'

const PAGE_SIZES: readonly number[] = PAGE_SIZE_OPTIONS
const SORTS = [
  { value: 'employee', label: 'Employee name' },
  { value: 'gross', label: 'Gross salary' },
  { value: 'net', label: 'Net salary' },
] as const

/** Records of one period with server-side search and filters (kept in the URL), view, edit and payslip actions. */
export default function PayrollRecordsPage() {
  const { id = '' } = useParams()
  const [params, setParams] = useSearchParams()
  const [searchInput, setSearchInput] = useState(params.get('search') ?? '')
  const [viewing, setViewing] = useState<PayrollRecord | null>(null)
  const [editing, setEditing] = useState<PayrollRecord | null>(null)

  const search = params.get('search') ?? ''
  const departmentId = params.get('departmentId') ?? ''
  const designationId = params.get('designationId') ?? ''
  const statusParam = params.get('status') ?? ''
  const status = isPayrollRecordStatus(statusParam) ? statusParam : undefined
  const pageParam = Number(params.get('page'))
  const page = Number.isInteger(pageParam) && pageParam >= 1 ? pageParam : 1
  const pageSize = PAGE_SIZES.includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 25
  const sortParam = params.get('sortBy') ?? ''
  const sortBy = SORTS.find((s) => s.value === sortParam)?.value
  const directionParam = params.get('sortDirection')
  const direction: 'asc' | 'desc' | undefined = directionParam === 'asc' || directionParam === 'desc' ? directionParam : undefined

  const setFilter = useCallback(
    (key: string, value: string) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          if (value) next.set(key, value)
          else next.delete(key)
          if (key !== 'page') next.delete('page')
          return next
        },
        { replace: true },
      ),
    [setParams],
  )

  // Search is sent to the server after a short pause in typing.
  useEffect(() => {
    if (searchInput.trim() === search) return
    const timer = window.setTimeout(() => setFilter('search', searchInput.trim()), 350)
    return () => window.clearTimeout(timer)
  }, [searchInput, search, setFilter])

  const loadPeriod = useCallback((signal: AbortSignal) => Promise.all([getPayrollPeriod(id, signal), getDepartments(signal), getDesignations(signal)]), [id])
  const { data: meta, error: metaError, reload: reloadPeriod } = useApi(loadPeriod)
  const [period, departments, designations] = meta ?? [undefined, [], []]

  // Filtered, sorted and paged on the server (the Day 17 history endpoint, limited to this period).
  const query = useMemo(
    () => ({
      payrollPeriodId: id,
      search: search || undefined,
      departmentId: departmentId || undefined,
      designationId: designationId || undefined,
      recordStatus: status,
      page,
      pageSize,
      sortBy: sortBy ?? 'employee',
      sortDirection: direction,
    }),
    [id, search, departmentId, designationId, status, page, pageSize, sortBy, direction],
  )
  const loadRecords = useCallback((signal: AbortSignal) => getPayrollHistory(query, signal), [query])
  const { data: records, error, loading, reload } = useApi(loadRecords)
  const rows = records?.items ?? []
  const filtered = !!(search || departmentId || designationId || status)

  const reset = () => {
    setSearchInput('')
    setParams(new URLSearchParams(), { replace: true })
  }

  const refresh = () => {
    reload()
    reloadPeriod()
  }

  if (metaError) return <div className="card bg-base-100 shadow-sm"><ErrorState error={metaError} onRetry={reloadPeriod} /></div>

  return (
    <>
      <PageHeader
        title={period ? `${period.name} — records` : 'Payroll records'}
        description="Each employee's earnings, deductions and net salary for this period."
        actions={
          period && (
            <>
              <PayrollStatusBadge status={period.status} />
              {period.isLocked && <LockedBadge />}
              <ExportButtons
                label="Export records"
                onExport={(format) =>
                  exportPayrollReport('employees', { payrollPeriodId: id, status: period.status, search: search || undefined, departmentId: departmentId || undefined, designationId: designationId || undefined }, format)
                }
              />
              <Link to={`/payroll/${id}`} className="btn btn-sm">
                Period details
              </Link>
            </>
          )
        }
      />

      {period && (
        <div className="mb-6 flex flex-col gap-4">
          <PayrollSummaryCards employees={period.employeeCount} gross={period.grossTotal} deduction={period.deductionTotal} net={period.netTotal} />
          {period.isLocked && (
            <div role="note" className="alert alert-soft">
              <span className="text-sm">This payroll is finalized and locked: the records below are read-only.</span>
            </div>
          )}
        </div>
      )}

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <label className="input input-sm w-full">
              <Search className="size-4 opacity-50" />
              <input type="search" placeholder="Search name or employee ID" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} aria-label="Search" />
            </label>
            <select className="select select-sm w-full" value={departmentId} onChange={(e) => setFilter('departmentId', e.target.value)} aria-label="Department">
              <option value="">All departments</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
            <select className="select select-sm w-full" value={designationId} onChange={(e) => setFilter('designationId', e.target.value)} aria-label="Designation">
              <option value="">All designations</option>
              {designations.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
            <select className="select select-sm w-full" value={status ?? ''} onChange={(e) => setFilter('status', e.target.value)} aria-label="Status">
              <option value="">All statuses</option>
              {PAYROLL_RECORD_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {enumLabel(s)}
                </option>
              ))}
            </select>
            <div className="grid grid-cols-2 gap-2">
              <select className="select select-sm w-full" value={sortBy ?? 'employee'} onChange={(e) => setFilter('sortBy', e.target.value === 'employee' ? '' : e.target.value)} aria-label="Sort by">
                {SORTS.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
              <select className="select select-sm w-full" value={direction ?? ''} onChange={(e) => setFilter('sortDirection', e.target.value)} aria-label="Sort direction">
                <option value="">Default order</option>
                <option value="asc">Ascending</option>
                <option value="desc">Descending</option>
              </select>
            </div>
            <button type="button" className="btn btn-sm btn-ghost justify-self-start" onClick={reset} disabled={!filtered && !sortBy && !direction}>
              <RotateCcw className="size-4" /> Reset filters
            </button>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !records ? (
            <Loading label="Loading payroll records…" />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Users}
              title={filtered ? 'No records match these filters' : 'No payroll records yet'}
              description={filtered ? 'Try another search or filter.' : 'Calculate the payroll from the period page to create the records.'}
              action={!filtered && <Link to={`/payroll/${id}`} className="btn btn-sm">Go to period</Link>}
            />
          ) : (
            <>
              <p className="text-sm text-base-content/60">
                {records?.totalCount ?? 0} records
                {loading && <span className="loading loading-spinner loading-xs ml-2 align-middle" />}
              </p>
              <PayrollRecordTable records={rows} onView={setViewing} onEdit={setEditing} showPayment showBreakdown />
              <Pagination
                page={records?.page ?? 1}
                pageCount={Math.max(1, records?.totalPages ?? 1)}
                pageSize={records?.pageSize ?? pageSize}
                total={records?.totalCount ?? 0}
                onPageChange={(next) => setFilter('page', next === 1 ? '' : String(next))}
                onPageSizeChange={(size) => setFilter('pageSize', String(size))}
              />
            </>
          )}
        </div>
      </div>

      {viewing && <PayrollRecordModal record={viewing} onClose={() => setViewing(null)} />}
      {editing && <PayrollRecordEditModal record={editing} onClose={() => setEditing(null)} onSaved={refresh} />}
    </>
  )
}
