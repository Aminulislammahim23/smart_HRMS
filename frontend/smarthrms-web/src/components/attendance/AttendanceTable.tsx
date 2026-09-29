import { CalendarDays, Eye } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { Attendance } from '../../types/attendance'
import { formatDate, formatDuration, formatTime } from '../../utils/formatters'
import { Table, type Column } from '../common/Table'
import { AttendanceStatusBadge } from './AttendanceStatusBadge'

interface AttendanceTableProps {
  records: readonly Attendance[]
  onView: (record: Attendance) => void
  /** Hide the employee columns on a single employee's page. */
  showEmployee?: boolean
  /** Hide the date column when every row is the same day (dashboard). */
  showDate?: boolean
}

export function AttendanceTable({ records, onView, showEmployee = true, showDate = true }: AttendanceTableProps) {
  const columns: Column<Attendance>[] = [
    ...(showDate ? [{ key: 'date', header: 'Date', className: 'whitespace-nowrap', render: (r: Attendance) => formatDate(r.attendanceDate) }] : []),
    ...(showEmployee
      ? [
          {
            key: 'employee',
            header: 'Employee',
            render: (r: Attendance) => (
              <Link to={`/attendance/employee/${r.employeeId}`} className="block hover:text-primary">
                <span className="font-medium">{r.employeeName}</span>
                {/* Below xl the Employee ID column is hidden, so show the code here. */}
                <span className="block text-xs text-base-content/60 xl:hidden">{r.employeeCode}</span>
              </Link>
            ),
          },
          { key: 'code', header: 'Employee ID', className: 'hidden xl:table-cell whitespace-nowrap', render: (r: Attendance) => r.employeeCode },
        ]
      : []),
    { key: 'in', header: 'Check in', className: 'whitespace-nowrap', render: (r) => formatTime(r.checkInTime) },
    { key: 'out', header: 'Check out', className: 'whitespace-nowrap', render: (r) => formatTime(r.checkOutTime) },
    { key: 'hours', header: 'Working hours', className: 'hidden sm:table-cell whitespace-nowrap', render: (r) => formatDuration(r.workingMinutes) },
    { key: 'status', header: 'Status', render: (r) => <AttendanceStatusBadge status={r.status} /> },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (r) => (
        <div className="flex justify-end gap-1">
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => onView(r)} title="Details" aria-label={`Details for ${r.employeeName} on ${r.attendanceDate}`}>
            <Eye className="size-4" />
          </button>
          {showEmployee && (
            <Link to={`/attendance/employee/${r.employeeId}?month=${r.attendanceDate.slice(0, 7)}`} className="btn btn-ghost btn-xs btn-square" title="Monthly attendance" aria-label={`Monthly attendance of ${r.employeeName}`}>
              <CalendarDays className="size-4" />
            </Link>
          )}
        </div>
      ),
    },
  ]

  return <Table columns={columns} rows={records} rowKey={(r) => r.id} compact />
}
