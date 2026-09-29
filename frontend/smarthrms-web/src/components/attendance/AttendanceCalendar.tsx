import type { Attendance } from '../../types/attendance'
import { enumLabel, formatTime, todayInput, toIsoDate } from '../../utils/formatters'
import { STATUS_CELL } from './attendanceStyles'

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

interface AttendanceCalendarProps {
  /** "yyyy-MM" */
  month: string
  records: Attendance[]
  onSelectDay: (date: string, record: Attendance | undefined) => void
}

/** Month grid: each day shows the recorded status and times; days without a record stay blank. */
export function AttendanceCalendar({ month, records, onSelectDay }: AttendanceCalendarProps) {
  const [year, monthIndex] = month.split('-').map(Number)
  const first = new Date(year, monthIndex - 1, 1)
  const daysInMonth = new Date(year, monthIndex, 0).getDate()
  const byDate = new Map(records.map((record) => [record.attendanceDate, record]))
  const today = todayInput()

  const cells: (string | null)[] = [
    ...Array.from({ length: first.getDay() }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => toIsoDate(new Date(year, monthIndex - 1, i + 1))),
  ]

  return (
    <div>
      <div className="grid grid-cols-7 gap-1 text-center text-xs font-medium text-base-content/60">
        {WEEKDAYS.map((day) => (
          <div key={day} className="py-1">
            {day}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7 gap-1" role="grid" aria-label="Attendance calendar">
        {cells.map((date, index) => {
          if (!date) return <div key={`blank-${index}`} />
          const record = byDate.get(date)
          return (
            <button
              key={date}
              type="button"
              onClick={() => onSelectDay(date, record)}
              aria-label={`${date}: ${record ? enumLabel(record.status) : 'no record'}`}
              className={`flex min-h-16 flex-col items-start rounded-lg border p-1.5 text-left text-xs transition hover:border-primary sm:min-h-20 ${
                record ? STATUS_CELL[record.status] : 'border-base-300'
              } ${date === today ? 'ring-2 ring-primary' : ''}`}
            >
              <span className="font-semibold">{Number(date.slice(8))}</span>
              {record && (
                <>
                  <span className="mt-auto hidden truncate sm:block">{enumLabel(record.status)}</span>
                  {record.checkInTime && (
                    <span className="hidden text-[10px] text-base-content/70 md:block">
                      {formatTime(record.checkInTime)}–{formatTime(record.checkOutTime)}
                    </span>
                  )}
                </>
              )}
            </button>
          )
        })}
      </div>
    </div>
  )
}
