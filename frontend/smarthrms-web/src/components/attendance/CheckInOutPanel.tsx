import { LogIn, LogOut } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '../../hooks/useToast'
import { checkIn, checkOut } from '../../services/attendanceService'
import type { Employee } from '../../types/employee'
import { errorMessage } from '../../utils/errors'
import { enumLabel, formatTime } from '../../utils/formatters'

interface CheckInOutPanelProps {
  employees: Employee[]
  onDone: () => void
}

/**
 * Check-in / check-out for today at the server's current office time. The backend has no sign-in yet, so the
 * employee is chosen here; once authentication exists this becomes "check in as me".
 */
export function CheckInOutPanel({ employees, onDone }: CheckInOutPanelProps) {
  const { notify } = useToast()
  const [employeeId, setEmployeeId] = useState('')
  const [busy, setBusy] = useState<'in' | 'out' | null>(null)

  const run = async (kind: 'in' | 'out') => {
    setBusy(kind)
    try {
      const record = kind === 'in' ? await checkIn({ employeeId }) : await checkOut({ employeeId })
      notify(
        'success',
        kind === 'in'
          ? `${record.employeeName} checked in at ${formatTime(record.checkInTime)} (${enumLabel(record.status)}).`
          : `${record.employeeName} checked out at ${formatTime(record.checkOutTime)}.`,
      )
      onDone()
    } catch (error) {
      notify('error', errorMessage(error))
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-3">
        <div>
          <h2 className="font-semibold">Check in / check out</h2>
          <p className="text-sm text-base-content/60">Recorded for today at the server's office time.</p>
        </div>
        <div className="flex flex-col gap-2 sm:flex-row">
          <select className="select w-full sm:max-w-sm" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} aria-label="Employee to check in or out">
            <option value="">Select an employee</option>
            {employees.map((employee) => (
              <option key={employee.id} value={employee.id}>
                {employee.fullName} ({employee.employeeCode})
              </option>
            ))}
          </select>
          <div className="flex gap-2">
            <button type="button" className="btn btn-primary" disabled={!employeeId || busy !== null} onClick={() => run('in')}>
              {busy === 'in' ? <span className="loading loading-spinner loading-sm" /> : <LogIn className="size-4" />} Check in
            </button>
            <button type="button" className="btn" disabled={!employeeId || busy !== null} onClick={() => run('out')}>
              {busy === 'out' ? <span className="loading loading-spinner loading-sm" /> : <LogOut className="size-4" />} Check out
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
