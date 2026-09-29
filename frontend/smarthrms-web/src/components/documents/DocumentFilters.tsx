import { Search } from 'lucide-react'
import { DOCUMENT_TYPES } from '../../types/document'
import type { Employee } from '../../types/employee'
import { enumLabel } from '../../utils/formatters'

export interface DocumentFilterValues {
  search: string
  employeeId: string
  documentType: string
  showInactive: boolean
}

interface DocumentFiltersProps {
  values: DocumentFilterValues
  /** Receives only the changed fields, so quick successive changes can't overwrite each other. */
  onChange: (patch: Partial<DocumentFilterValues>) => void
  /** Employee filter is shown only when a list is given (cross-employee pages). */
  employees?: Employee[]
}

export function DocumentFilters({ values, onChange, employees }: DocumentFiltersProps) {
  const set = onChange

  return (
    <div className="flex flex-col gap-2 lg:flex-row lg:items-center">
      <label className="input input-sm w-full lg:max-w-xs">
        <Search className="size-4 opacity-50" />
        <input type="search" placeholder="Search name, file, description" value={values.search} onChange={(e) => set({ search: e.target.value })} aria-label="Search documents" />
      </label>
      {employees && (
        <select className="select select-sm w-full lg:w-56" value={values.employeeId} onChange={(e) => set({ employeeId: e.target.value })} aria-label="Employee">
          <option value="">All employees</option>
          {employees.map((employee) => (
            <option key={employee.id} value={employee.id}>
              {employee.fullName} ({employee.employeeCode})
            </option>
          ))}
        </select>
      )}
      <select className="select select-sm w-full lg:w-52" value={values.documentType} onChange={(e) => set({ documentType: e.target.value })} aria-label="Document type">
        <option value="">All document types</option>
        {DOCUMENT_TYPES.map((type) => (
          <option key={type} value={type}>
            {enumLabel(type)}
          </option>
        ))}
      </select>
      <label className="label cursor-pointer gap-2 text-sm lg:ml-auto">
        <input type="checkbox" className="toggle toggle-sm" checked={values.showInactive} onChange={(e) => set({ showInactive: e.target.checked })} />
        Show deactivated
      </label>
    </div>
  )
}
