import { FilterX, Search, UserPlus, Users } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ConfirmDialog } from '../../components/common/ConfirmDialog'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import type { SortState } from '../../components/common/Table'
import { EmployeeTable } from '../../components/employee/EmployeeTable'
import { useApi } from '../../hooks/useApi'
import { usePagination } from '../../hooks/usePagination'
import { useToast } from '../../hooks/useToast'
import { getDepartments } from '../../services/departmentService'
import { getDesignations } from '../../services/designationService'
import { deactivateEmployee, getEmployees } from '../../services/employeeService'
import { EMPLOYEE_STATUSES, EMPLOYMENT_TYPES, type Employee } from '../../types/employee'
import { enumLabel } from '../../utils/formatters'

const FILTER_KEYS = ['q', 'department', 'designation', 'status', 'type'] as const
type FilterKey = (typeof FILTER_KEYS)[number]

const SORTERS: Record<string, (a: Employee, b: Employee) => number> = {
  name: (a, b) => a.fullName.localeCompare(b.fullName),
  code: (a, b) => a.employeeCode.localeCompare(b.employeeCode, undefined, { numeric: true }),
  department: (a, b) => (a.departmentName ?? '').localeCompare(b.departmentName ?? ''),
  joiningDate: (a, b) => a.joiningDate.localeCompare(b.joiningDate),
  status: (a, b) => a.status.localeCompare(b.status),
}

/**
 * The backend returns every employee in one response and has no search, filter, sort or paging parameters,
 * so those run here in the browser. Filters live in the URL so they survive navigating to an employee and back.
 */
export default function EmployeeList() {
  const { notify } = useToast()
  const [params, setParams] = useSearchParams()
  const [sort, setSort] = useState<SortState>({ key: 'name', direction: 'asc' })
  const [pendingDeactivate, setPendingDeactivate] = useState<Employee | null>(null)

  const load = useCallback(
    (signal: AbortSignal) => Promise.all([getEmployees(signal), getDepartments(signal), getDesignations(signal)]),
    [],
  )
  const { data, error, loading, reload } = useApi(load)
  const [employees, departments, designations] = data ?? [[], [], []]

  const filter = (key: FilterKey) => params.get(key) ?? ''
  const hasFilters = FILTER_KEYS.some((key) => params.get(key))

  const visible = useMemo(() => {
    const term = (params.get('q') ?? '').trim().toLowerCase()
    const department = params.get('department')
    const designation = params.get('designation')
    const status = params.get('status')
    const type = params.get('type')

    const matches = employees.filter(
      (employee) =>
        (!term ||
          [employee.fullName, employee.employeeCode, employee.email, employee.phone ?? ''].some((value) =>
            value.toLowerCase().includes(term),
          )) &&
        (!department || employee.departmentId === department) &&
        (!designation || employee.designationId === designation) &&
        (!status || (status === 'current' ? employee.isActive : employee.status === status)) &&
        (!type || employee.employmentType === type),
    )

    const compare = SORTERS[sort.key] ?? SORTERS.name
    return matches.sort((a, b) => (sort.direction === 'asc' ? compare(a, b) : compare(b, a)))
  }, [employees, params, sort])

  const pagination = usePagination(visible)

  const setFilter = (key: FilterKey, value: string) => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (value) next.set(key, value)
        else next.delete(key)
        return next
      },
      { replace: true },
    )
    pagination.setPage(1)
  }

  return (
    <>
      <PageHeader
        title="Employees"
        description={data ? `${employees.length} employees in total, ${employees.filter((e) => e.isActive).length} currently working.` : undefined}
        actions={
          <Link to="/employees/create" className="btn btn-primary btn-sm">
            <UserPlus className="size-4" /> Add employee
          </Link>
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-6">
            <label className="input w-full lg:col-span-2">
              <Search className="size-4 opacity-50" />
              <input
                type="search"
                placeholder="Search name, code, email or phone"
                value={filter('q')}
                onChange={(event) => setFilter('q', event.target.value)}
              />
            </label>
            <select className="select w-full" value={filter('department')} onChange={(e) => setFilter('department', e.target.value)} aria-label="Department">
              <option value="">All departments</option>
              {departments.map((department) => (
                <option key={department.id} value={department.id}>
                  {department.name}
                  {department.isActive ? '' : ' (inactive)'}
                </option>
              ))}
            </select>
            <select className="select w-full" value={filter('designation')} onChange={(e) => setFilter('designation', e.target.value)} aria-label="Designation">
              <option value="">All designations</option>
              {designations.map((designation) => (
                <option key={designation.id} value={designation.id}>
                  {designation.name}
                  {designation.isActive ? '' : ' (inactive)'}
                </option>
              ))}
            </select>
            <select className="select w-full" value={filter('status')} onChange={(e) => setFilter('status', e.target.value)} aria-label="Status">
              <option value="">All statuses</option>
              <option value="current">Currently working</option>
              {EMPLOYEE_STATUSES.map((status) => (
                <option key={status} value={status}>
                  {enumLabel(status)}
                </option>
              ))}
            </select>
            <select className="select w-full" value={filter('type')} onChange={(e) => setFilter('type', e.target.value)} aria-label="Employment type">
              <option value="">All types</option>
              {EMPLOYMENT_TYPES.map((type) => (
                <option key={type} value={type}>
                  {enumLabel(type)}
                </option>
              ))}
            </select>
          </div>

          {hasFilters && (
            <div className="flex items-center justify-between text-sm text-base-content/70">
              <span>
                {visible.length} of {employees.length} employees match
              </span>
              <button
                type="button"
                className="btn btn-ghost btn-xs"
                onClick={() => {
                  setParams({}, { replace: true })
                  pagination.setPage(1)
                }}
              >
                <FilterX className="size-3.5" /> Clear filters
              </button>
            </div>
          )}

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label="Loading employees…" />
          ) : visible.length === 0 ? (
            <EmptyState
              icon={Users}
              title={employees.length ? 'No employees match your filters' : 'No employees yet'}
              description={employees.length ? 'Try a different search or clear the filters.' : 'Add the first employee to get started.'}
              action={
                !employees.length && (
                  <Link to="/employees/create" className="btn btn-primary btn-sm">
                    <UserPlus className="size-4" /> Add employee
                  </Link>
                )
              }
            />
          ) : (
            <>
              <EmployeeTable employees={pagination.pageItems} sort={sort} onSortChange={setSort} onDeactivate={setPendingDeactivate} />
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

      <ConfirmDialog
        open={pendingDeactivate !== null}
        title="Deactivate employee?"
        message={`${pendingDeactivate?.fullName} (${pendingDeactivate?.employeeCode}) will be marked Inactive. The record is kept and can be reactivated from the edit page.`}
        confirmLabel="Deactivate"
        onCancel={() => setPendingDeactivate(null)}
        onConfirm={async () => {
          if (!pendingDeactivate) return
          const message = await deactivateEmployee(pendingDeactivate.id)
          notify('success', message)
          setPendingDeactivate(null)
          reload()
        }}
      />
    </>
  )
}
