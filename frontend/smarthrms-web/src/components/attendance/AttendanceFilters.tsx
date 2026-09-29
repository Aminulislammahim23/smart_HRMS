import { FilterX } from 'lucide-react'
import type { Department } from '../../types/department'
import type { Employee } from '../../types/employee'
import { ATTENDANCE_STATUSES } from '../../types/attendance'
import { enumLabel } from '../../utils/formatters'

export interface AttendanceFilterValues {
  mode: 'date' | 'range'
  date: string
  startDate: string
  endDate: string
  employeeId: string
  /** Applied in the browser: the attendance API has no department filter. */
  departmentId: string
  status: string
}

interface AttendanceFiltersProps {
  values: AttendanceFilterValues
  /** Receives only the changed fields, so quick successive changes can't overwrite each other. */
  onChange: (patch: Partial<AttendanceFilterValues>) => void
  onReset: () => void
  employees: Employee[]
  departments: Department[]
}

export function AttendanceFilters({ values, onChange, onReset, employees, departments }: AttendanceFiltersProps) {
  const set = onChange

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-end gap-3">
        <div className="join" role="group" aria-label="Date filter type">
          <button type="button" className={`btn btn-sm join-item ${values.mode === 'date' ? 'btn-primary' : ''}`} onClick={() => set({ mode: 'date' })}>
            Single date
          </button>
          <button type="button" className={`btn btn-sm join-item ${values.mode === 'range' ? 'btn-primary' : ''}`} onClick={() => set({ mode: 'range' })}>
            Date range
          </button>
        </div>
        {values.mode === 'date' ? (
          <label className="flex flex-col gap-1 text-xs">
            Date
            <input type="date" className="input input-sm w-44" value={values.date} onChange={(e) => set({ date: e.target.value })} aria-label="Date" />
          </label>
        ) : (
          <>
            <label className="flex flex-col gap-1 text-xs">
              From
              <input type="date" className="input input-sm w-44" value={values.startDate} max={values.endDate || undefined} onChange={(e) => set({ startDate: e.target.value })} aria-label="Start date" />
            </label>
            <label className="flex flex-col gap-1 text-xs">
              To
              <input type="date" className="input input-sm w-44" value={values.endDate} min={values.startDate || undefined} onChange={(e) => set({ endDate: e.target.value })} aria-label="End date" />
            </label>
          </>
        )}
      </div>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
        <select className="select select-sm w-full" value={values.employeeId} onChange={(e) => set({ employeeId: e.target.value })} aria-label="Employee">
          <option value="">All employees</option>
          {employees.map((employee) => (
            <option key={employee.id} value={employee.id}>
              {employee.fullName} ({employee.employeeCode})
            </option>
          ))}
        </select>
        <select className="select select-sm w-full" value={values.departmentId} onChange={(e) => set({ departmentId: e.target.value })} aria-label="Department">
          <option value="">All departments</option>
          {departments.map((department) => (
            <option key={department.id} value={department.id}>
              {department.name}
            </option>
          ))}
        </select>
        <select className="select select-sm w-full" value={values.status} onChange={(e) => set({ status: e.target.value })} aria-label="Status">
          <option value="">All statuses</option>
          {ATTENDANCE_STATUSES.map((status) => (
            <option key={status} value={status}>
              {enumLabel(status)}
            </option>
          ))}
        </select>
        <button type="button" className="btn btn-sm btn-ghost justify-self-start" onClick={onReset}>
          <FilterX className="size-4" /> Reset filters
        </button>
      </div>
    </div>
  )
}
