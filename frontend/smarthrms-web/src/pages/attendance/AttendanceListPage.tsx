import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { AttendanceDetailsModal } from '../../components/attendance/AttendanceDetailsModal'
import { AttendanceFilters, type AttendanceFilterValues } from '../../components/attendance/AttendanceFilters'
import { AttendanceTable } from '../../components/attendance/AttendanceTable'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { useApi } from '../../hooks/useApi'
import { usePagination } from '../../hooks/usePagination'
import { getAttendance } from '../../services/attendanceService'
import { getDepartments } from '../../services/departmentService'
import { getEmployees } from '../../services/employeeService'
import { isAttendanceStatus, type Attendance, type AttendanceListQuery } from '../../types/attendance'
import { monthRange, todayInput } from '../../utils/formatters'

function defaults(): AttendanceFilterValues {
  const { start, end } = monthRange(todayInput().slice(0, 7))
  return { mode: 'range', date: todayInput(), startDate: start, endDate: end, employeeId: '', departmentId: '', status: '' }
}

function fromParams(params: URLSearchParams): AttendanceFilterValues {
  const base = defaults()
  const read = (key: keyof AttendanceFilterValues) => params.get(key) ?? base[key]
  return {
    mode: params.get('mode') === 'date' ? 'date' : 'range',
    date: read('date'),
    startDate: read('startDate'),
    endDate: read('endDate'),
    employeeId: read('employeeId'),
    departmentId: read('departmentId'),
    status: read('status'),
  }
}

/** Only non-empty values go into the URL. */
function toParams(values: AttendanceFilterValues): URLSearchParams {
  return new URLSearchParams(Object.entries(values).filter(([, value]) => value !== ''))
}

/**
 * All attendance records with filters. Employee, date/range and status are sent to the API; department is applied
 * in the browser because the attendance API has no department filter. Defaults to the current month so the list
 * never loads the whole history at once.
 */
export default function AttendanceListPage() {
  const [params, setParams] = useSearchParams()
  // Filters live in React state (whose functional updates are queued) and are mirrored into the URL so a filtered
  // view can be bookmarked or shared. React Router's setSearchParams(fn) is not queued, so quick successive changes
  // made through it directly could overwrite each other.
  const [filters, setFilters] = useState(() => fromParams(params))
  const [viewing, setViewing] = useState<Attendance | null>(null)

  const serialized = toParams(filters).toString()
  useEffect(() => {
    if (serialized !== params.toString()) setParams(new URLSearchParams(serialized), { replace: true })
  }, [serialized, params, setParams])

  // Only these values are sent to the API; the query is rebuilt (and refetched) only when one of them changes.
  const { mode, date, startDate, endDate, employeeId, status } = filters
  const query = useMemo<AttendanceListQuery>(
    () => ({
      employeeId: employeeId || undefined,
      status: isAttendanceStatus(status) ? status : undefined,
      ...(mode === 'date' ? { date: date || undefined } : { startDate: startDate || undefined, endDate: endDate || undefined }),
    }),
    [mode, date, startDate, endDate, employeeId, status],
  )

  const lookups = useCallback((signal: AbortSignal) => Promise.all([getEmployees(signal), getDepartments(signal)]), [])
  const { data: lookupData } = useApi(lookups)
  const [employees, departments] = lookupData ?? [[], []]

  const load = useCallback((signal: AbortSignal) => getAttendance(query, signal), [query])
  const { data: records, error, loading, reload } = useApi(load)

  const departmentOf = useMemo(() => new Map(employees.map((employee) => [employee.id, employee.departmentId])), [employees])
  const visible = useMemo(
    () => (records ?? []).filter((record) => !filters.departmentId || departmentOf.get(record.employeeId) === filters.departmentId),
    [records, filters.departmentId, departmentOf],
  )
  const pagination = usePagination(visible, 25)

  const update = (patch: Partial<AttendanceFilterValues>) => {
    setFilters((current) => ({ ...current, ...patch }))
    pagination.setPage(1)
  }

  return (
    <>
      <PageHeader title="Attendance records" description="Search attendance by date, employee, department and status." />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <AttendanceFilters values={filters} onChange={update} onReset={() => update(defaults())} employees={employees} departments={departments} />

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !records ? (
            <Loading label="Loading attendance…" />
          ) : visible.length === 0 ? (
            <EmptyState title="No attendance records match these filters" description="Try another date range, employee or status." />
          ) : (
            <>
              <p className="text-sm text-base-content/60">{visible.length} records</p>
              <AttendanceTable records={pagination.pageItems} onView={setViewing} />
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

      {viewing && <AttendanceDetailsModal record={viewing} onClose={() => setViewing(null)} onChanged={reload} />}
    </>
  )
}
