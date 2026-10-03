import { ArrowLeft, ChevronLeft, ChevronRight } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { AttendanceCalendar } from '../../components/attendance/AttendanceCalendar'
import { AttendanceDetailsModal } from '../../components/attendance/AttendanceDetailsModal'
import { AttendanceForm } from '../../components/attendance/AttendanceForm'
import { AttendanceStatusBadge } from '../../components/attendance/AttendanceStatusBadge'
import { AttendanceTable } from '../../components/attendance/AttendanceTable'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { Modal } from '../../components/common/Modal'
import { EmployeePhoto } from '../../components/employee/EmployeePhoto'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { useToast } from '../../hooks/useToast'
import { createAttendance, getEmployeeAttendance } from '../../services/attendanceService'
import { getEmployeeById } from '../../services/employeeService'
import { ATTENDANCE_STATUSES, type Attendance } from '../../types/attendance'
import { formatDate, formatDuration, formatMonth, monthRange, shiftMonth, todayInput } from '../../utils/formatters'

/** One employee's month: calendar, totals per status, and the day-by-day history. */
export default function EmployeeAttendancePage() {
  const { employeeId = '' } = useParams()
  const [params, setParams] = useSearchParams()
  const { hasRole } = useAuth()
  const isHr = hasRole('HR', 'Admin')
  const { notify } = useToast()
  const requestedMonth = params.get('month') ?? ''
  const month = /^\d{4}-\d{2}$/.test(requestedMonth) ? requestedMonth : todayInput().slice(0, 7)
  const [viewing, setViewing] = useState<Attendance | null>(null)
  const [creatingFor, setCreatingFor] = useState<string | null>(null)

  const load = useCallback(
    (signal: AbortSignal) => {
      const { start, end } = monthRange(month)
      return Promise.all([getEmployeeById(employeeId, signal), getEmployeeAttendance(employeeId, { startDate: start, endDate: end }, signal)])
    },
    [employeeId, month],
  )
  const { data, error, loading, reload } = useApi(load)
  const [employee, records] = data ?? [undefined, []]

  const setMonth = (value: string) => setParams({ month: value }, { replace: true })
  const totalMinutes = records.reduce((sum, record) => sum + (record.workingMinutes ?? 0), 0)

  if (error) {
    return (
      <div className="card bg-base-100 shadow-sm">
        <ErrorState
          error={error}
          onRetry={reload}
          action={
            <Link to="/attendance" className="btn btn-sm btn-ghost">
              <ArrowLeft className="size-4" /> Back to attendance
            </Link>
          }
        />
      </div>
    )
  }

  if (loading && !data) return <Loading label="Loading attendance…" />
  if (!employee) return null

  return (
    <div className="flex flex-col gap-6">
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body flex-col gap-4 sm:flex-row sm:items-center">
          <EmployeePhoto photoUrl={employee.photoUrl} firstName={employee.firstName} lastName={employee.lastName} size="lg" />
          <div className="flex-1">
            <h1 className="text-xl font-semibold">{employee.fullName}</h1>
            <p className="text-sm text-base-content/60">
              {employee.employeeCode} · {employee.designationName} · {employee.departmentName}
            </p>
          </div>
          {isHr && (
            <div className="flex gap-2">
              <Link to="/attendance" className="btn btn-sm btn-ghost">
                <ArrowLeft className="size-4" /> Attendance
              </Link>
              <Link to={`/employees/${employee.id}`} className="btn btn-sm">
                Employee profile
              </Link>
            </div>
          )}
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-3">
        <div className="card bg-base-100 shadow-sm xl:col-span-2">
          <div className="card-body gap-4">
            <div className="flex items-center justify-between">
              <button type="button" className="btn btn-ghost btn-sm btn-square" onClick={() => setMonth(shiftMonth(month, -1))} aria-label="Previous month">
                <ChevronLeft className="size-4" />
              </button>
              <h2 className="font-semibold">{formatMonth(month)}</h2>
              <button type="button" className="btn btn-ghost btn-sm btn-square" onClick={() => setMonth(shiftMonth(month, 1))} aria-label="Next month">
                <ChevronRight className="size-4" />
              </button>
            </div>
            <AttendanceCalendar
              month={month}
              records={records}
              onSelectDay={(date, record) => (record ? setViewing(record) : isHr && employee.isActive && setCreatingFor(date))}
            />
            <p className="text-xs text-base-content/50">Select a day to see its details{employee.isActive ? ', or an empty day to add a record' : ''}.</p>
          </div>
        </div>

        <div className="card self-start bg-base-100 shadow-sm">
          <div className="card-body gap-3">
            <h2 className="font-semibold">Month summary</h2>
            <ul className="flex flex-col gap-2">
              {ATTENDANCE_STATUSES.map((status) => (
                <li key={status} className="flex items-center justify-between text-sm">
                  <AttendanceStatusBadge status={status} />
                  <span className="font-medium">{records.filter((record) => record.status === status).length}</span>
                </li>
              ))}
            </ul>
            <div className="divider my-1" />
            <p className="flex justify-between text-sm">
              <span>Days recorded</span>
              <span className="font-medium">{records.length}</span>
            </p>
            <p className="flex justify-between text-sm">
              <span>Total working hours</span>
              <span className="font-medium">{formatDuration(totalMinutes)}</span>
            </p>
          </div>
        </div>
      </div>

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <h2 className="font-semibold">History — {formatMonth(month)}</h2>
          {records.length === 0 ? (
            <EmptyState compact title="No attendance recorded this month" />
          ) : (
            <AttendanceTable records={records} onView={setViewing} showEmployee={false} />
          )}
        </div>
      </div>

      {viewing && <AttendanceDetailsModal record={viewing} onClose={() => setViewing(null)} onChanged={reload} readOnly={!isHr} />}

      <Modal open={creatingFor !== null} title={`Add record — ${creatingFor ? formatDate(creatingFor) : ''}`} onClose={() => setCreatingFor(null)} size="lg">
        {creatingFor !== null && (
          <AttendanceForm
            employees={[employee]}
            defaultEmployeeId={employee.id}
            defaultDate={creatingFor}
            onCancel={() => setCreatingFor(null)}
            onSubmit={async (request) => {
              await createAttendance(request)
              notify('success', 'Attendance recorded.')
              setCreatingFor(null)
              reload()
            }}
          />
        )}
      </Modal>
    </div>
  )
}
