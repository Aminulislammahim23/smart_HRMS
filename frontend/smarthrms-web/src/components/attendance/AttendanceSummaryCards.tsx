import { CalendarOff, CircleCheck, CircleSlash, Clock, HelpCircle, Hourglass, type LucideIcon } from 'lucide-react'
import type { Attendance, AttendanceStatus } from '../../types/attendance'

interface Card {
  status: AttendanceStatus | 'NotRecorded'
  label: string
  icon: LucideIcon
  tone: string
}

const CARDS: Card[] = [
  { status: 'Present', label: 'Present', icon: CircleCheck, tone: 'bg-success/15 text-success' },
  { status: 'Late', label: 'Late', icon: Clock, tone: 'bg-warning/15 text-warning' },
  { status: 'Absent', label: 'Absent', icon: CircleSlash, tone: 'bg-error/15 text-error' },
  { status: 'Leave', label: 'Leave', icon: CalendarOff, tone: 'bg-secondary/15 text-secondary' },
  { status: 'HalfDay', label: 'Half day', icon: Hourglass, tone: 'bg-info/15 text-info' },
  { status: 'NotRecorded', label: 'Not recorded', icon: HelpCircle, tone: 'bg-base-content/10 text-base-content/70' },
]

interface AttendanceSummaryCardsProps {
  /** Records of one day, straight from the API. */
  records: Attendance[]
  /** Current employees (Active or OnLeave): those without a record count as "Not recorded". */
  currentEmployeeIds: string[]
  onSelect?: (status: AttendanceStatus | 'NotRecorded') => void
  selected?: AttendanceStatus | 'NotRecorded' | ''
}

/** Counts per backend status for one day, plus current employees who have no record yet. */
export function AttendanceSummaryCards({ records, currentEmployeeIds, onSelect, selected }: AttendanceSummaryCardsProps) {
  const recorded = new Set(records.map((record) => record.employeeId))
  const count = (status: Card['status']) =>
    status === 'NotRecorded' ? currentEmployeeIds.filter((id) => !recorded.has(id)).length : records.filter((r) => r.status === status).length

  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
      {CARDS.map(({ status, label, icon: Icon, tone }) => (
        <button
          key={status}
          type="button"
          onClick={() => onSelect?.(status)}
          aria-pressed={selected === status}
          className={`card bg-base-100 text-left shadow-sm transition hover:shadow-md ${selected === status ? 'ring-2 ring-primary' : ''}`}
        >
          <div className="card-body flex-row items-center gap-3 p-4">
            <div className={`rounded-xl p-2.5 ${tone}`}>
              <Icon className="size-5" />
            </div>
            <div>
              <p className="text-2xl font-semibold leading-none">{count(status)}</p>
              <p className="mt-1 text-xs text-base-content/60">{label}</p>
            </div>
          </div>
        </button>
      ))}
    </div>
  )
}
