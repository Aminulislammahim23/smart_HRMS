import { ClipboardList, Plus } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { AttendanceDetailsModal } from '../../components/attendance/AttendanceDetailsModal'
import { AttendanceForm } from '../../components/attendance/AttendanceForm'
import { AttendanceSummaryCards } from '../../components/attendance/AttendanceSummaryCards'
import { AttendanceTable } from '../../components/attendance/AttendanceTable'
import { CheckInOutPanel } from '../../components/attendance/CheckInOutPanel'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { Modal } from '../../components/common/Modal'
import { PageHeader } from '../../components/common/PageHeader'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { createAttendance, getAttendance } from '../../services/attendanceService'
import { getEmployees } from '../../services/employeeService'
import type { Attendance, AttendanceStatus } from '../../types/attendance'
import { formatDate, todayInput } from '../../utils/formatters'

type Selection = AttendanceStatus | 'NotRecorded' | ''

/** One day at a glance: counts per status, check-in/out, and the day's records. */
export default function AttendanceDashboardPage() {
  const { notify } = useToast()
  const [date, setDate] = useState(todayInput)
  const [selected, setSelected] = useState<Selection>('')
  const [viewing, setViewing] = useState<Attendance | null>(null)
  const [creating, setCreating] = useState<{ employeeId?: string } | null>(null)

  const load = useCallback((signal: AbortSignal) => Promise.all([getAttendance({ date }, signal), getEmployees(signal)]), [date])
  const { data, error, loading, reload } = useApi(load)
  const [records, employees] = data ?? [[], []]

  const currentEmployees = useMemo(() => employees.filter((employee) => employee.isActive), [employees])
  const recordedIds = new Set(records.map((record) => record.employeeId))
  const notRecorded = currentEmployees.filter((employee) => !recordedIds.has(employee.id))
  const visible = selected && selected !== 'NotRecorded' ? records.filter((record) => record.status === selected) : records

  return (
    <>
      <PageHeader
        title="Attendance"
        description={`Attendance for ${formatDate(date)}.`}
        actions={
          <>
            <input type="date" className="input input-sm w-44" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} aria-label="Attendance date" />
            <Link to="/attendance/records" className="btn btn-sm">
              <ClipboardList className="size-4" /> All records
            </Link>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setCreating({})}>
              <Plus className="size-4" /> Add record
            </button>
          </>
        }
      />

      {error ? (
        <div className="card bg-base-100 shadow-sm">
          <ErrorState error={error} onRetry={reload} />
        </div>
      ) : loading && !data ? (
        <Loading label="Loading attendance…" />
      ) : (
        <div className="flex flex-col gap-6">
          <AttendanceSummaryCards
            records={records}
            currentEmployeeIds={currentEmployees.map((employee) => employee.id)}
            selected={selected}
            onSelect={(status) => setSelected((current) => (current === status ? '' : status))}
          />

          {date === todayInput() && <CheckInOutPanel employees={currentEmployees} onDone={reload} />}

          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-4 p-4 sm:p-6">
              <div className="flex items-center justify-between">
                <h2 className="font-semibold">{selected === 'NotRecorded' ? 'Current employees without a record' : 'Records'}</h2>
                {selected && (
                  <button type="button" className="btn btn-ghost btn-xs" onClick={() => setSelected('')}>
                    Show all
                  </button>
                )}
              </div>

              {selected === 'NotRecorded' ? (
                notRecorded.length === 0 ? (
                  <EmptyState compact title="Everyone has a record for this day" />
                ) : (
                  <ul className="divide-y divide-base-300">
                    {notRecorded.map((employee) => (
                      <li key={employee.id} className="flex items-center justify-between gap-3 py-2">
                        <span>
                          <span className="font-medium">{employee.fullName}</span>{' '}
                          <span className="text-sm text-base-content/60">{employee.employeeCode}</span>
                        </span>
                        <button type="button" className="btn btn-ghost btn-xs" onClick={() => setCreating({ employeeId: employee.id })}>
                          <Plus className="size-3.5" /> Record
                        </button>
                      </li>
                    ))}
                  </ul>
                )
              ) : visible.length === 0 ? (
                <EmptyState compact title={records.length ? 'No records with this status' : 'No attendance recorded for this day'} />
              ) : (
                <AttendanceTable records={visible} onView={setViewing} showDate={false} />
              )}
            </div>
          </div>
        </div>
      )}

      {viewing && <AttendanceDetailsModal record={viewing} onClose={() => setViewing(null)} onChanged={reload} />}

      <Modal open={creating !== null} title="Add attendance record" onClose={() => setCreating(null)} size="lg">
        {creating !== null && (
          <AttendanceForm
            employees={currentEmployees}
            defaultEmployeeId={creating.employeeId}
            defaultDate={date}
            onCancel={() => setCreating(null)}
            onSubmit={async (request) => {
              const created = await createAttendance(request)
              notify('success', `Attendance recorded for ${created.employeeName}.`)
              setCreating(null)
              reload()
            }}
          />
        )}
      </Modal>
    </>
  )
}
