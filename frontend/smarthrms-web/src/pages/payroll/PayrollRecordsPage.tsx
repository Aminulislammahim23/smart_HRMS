import { Search, Users } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { PayrollRecordEditModal } from '../../components/payroll/PayrollRecordEditModal'
import { PayrollRecordModal } from '../../components/payroll/PayrollRecordModal'
import { PayrollRecordTable } from '../../components/payroll/PayrollRecordTable'
import { PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { PayrollSummaryCards } from '../../components/payroll/PayrollSummaryCards'
import { useApi } from '../../hooks/useApi'
import { usePagination } from '../../hooks/usePagination'
import { getDepartments } from '../../services/departmentService'
import { getDesignations } from '../../services/designationService'
import { getPayrollPeriod, getPayrollRecords } from '../../services/payrollService'
import { PAYROLL_RECORD_STATUSES, isPayrollRecordStatus, type PayrollRecord } from '../../types/payroll'
import { enumLabel } from '../../utils/formatters'

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

  const setFilter = useCallback(
    (key: string, value: string) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          if (value) next.set(key, value)
          else next.delete(key)
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

  const loadRecords = useCallback(
    (signal: AbortSignal) => getPayrollRecords(id, { search: search || undefined, departmentId: departmentId || undefined, designationId: designationId || undefined, status }, signal),
    [id, search, departmentId, designationId, status],
  )
  const { data: records, error, loading, reload } = useApi(loadRecords)
  const rows = useMemo(() => records ?? [], [records])
  const pagination = usePagination(rows, 25)
  const filtered = !!(search || departmentId || designationId || status)

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
              <Link to={`/payroll/${id}`} className="btn btn-sm">
                Period details
              </Link>
            </>
          )
        }
      />

      {period && (
        <div className="mb-6">
          <PayrollSummaryCards employees={period.employeeCount} gross={period.grossTotal} deduction={period.deductionTotal} net={period.netTotal} />
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
              <p className="text-sm text-base-content/60">{rows.length} records</p>
              <PayrollRecordTable records={pagination.pageItems} onView={setViewing} onEdit={setEditing} />
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

      {viewing && <PayrollRecordModal record={viewing} onClose={() => setViewing(null)} />}
      {editing && <PayrollRecordEditModal record={editing} onClose={() => setEditing(null)} onSaved={refresh} />}
    </>
  )
}
