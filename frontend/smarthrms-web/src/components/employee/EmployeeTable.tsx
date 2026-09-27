import { Eye, Pencil, UserX } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { Employee } from '../../types/employee'
import { enumLabel, formatDate } from '../../utils/formatters'
import { EmployeeStatusBadge } from '../common/StatusBadge'
import { Table, type Column, type SortState } from '../common/Table'
import { EmployeePhoto } from './EmployeePhoto'

interface EmployeeTableProps {
  employees: readonly Employee[]
  sort: SortState
  onSortChange: (sort: SortState) => void
  onDeactivate: (employee: Employee) => void
}

export function EmployeeTable({ employees, sort, onSortChange, onDeactivate }: EmployeeTableProps) {
  const columns: Column<Employee>[] = [
    {
      key: 'name',
      header: 'Employee',
      sortable: true,
      render: (employee) => (
        <Link to={`/employees/${employee.id}`} className="flex min-w-48 items-center gap-3">
          <EmployeePhoto photoUrl={employee.photoUrl} firstName={employee.firstName} lastName={employee.lastName} size="sm" />
          <div className="min-w-0 max-w-44 xl:max-w-60">
            <p className="truncate font-medium hover:text-primary">{employee.fullName}</p>
            <p className="truncate text-xs text-base-content/60">
              <span className="xl:hidden">{employee.employeeCode} · </span>
              {employee.email}
            </p>
          </div>
        </Link>
      ),
    },
    { key: 'code', header: 'Code', sortable: true, className: 'hidden xl:table-cell whitespace-nowrap', render: (employee) => employee.employeeCode },
    {
      key: 'department',
      header: 'Position',
      sortable: true,
      className: 'hidden md:table-cell',
      render: (employee) => (
        <div className="min-w-36">
          <p>{employee.departmentName ?? '—'}</p>
          <p className="text-xs text-base-content/60">{employee.designationName ?? '—'}</p>
        </div>
      ),
    },
    {
      key: 'type',
      header: 'Type',
      className: 'hidden 2xl:table-cell whitespace-nowrap',
      render: (employee) => enumLabel(employee.employmentType),
    },
    {
      key: 'joiningDate',
      header: 'Joined',
      sortable: true,
      className: 'hidden xl:table-cell whitespace-nowrap',
      render: (employee) => formatDate(employee.joiningDate),
    },
    { key: 'status', header: 'Status', sortable: true, render: (employee) => <EmployeeStatusBadge status={employee.status} /> },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (employee) => (
        <div className="flex justify-end gap-1">
          <Link to={`/employees/${employee.id}`} className="btn btn-ghost btn-xs btn-square" title="View" aria-label={`View ${employee.fullName}`}>
            <Eye className="size-4" />
          </Link>
          <Link to={`/employees/${employee.id}/edit`} className="btn btn-ghost btn-xs btn-square" title="Edit" aria-label={`Edit ${employee.fullName}`}>
            <Pencil className="size-4" />
          </Link>
          {employee.status !== 'Inactive' && (
            <button
              type="button"
              className="btn btn-ghost btn-xs btn-square text-error"
              title="Deactivate"
              aria-label={`Deactivate ${employee.fullName}`}
              onClick={() => onDeactivate(employee)}
            >
              <UserX className="size-4" />
            </button>
          )}
        </div>
      ),
    },
  ]

  return <Table columns={columns} rows={employees} rowKey={(employee) => employee.id} sort={sort} onSortChange={onSortChange} />
}
