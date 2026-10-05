import { CalendarDays, ClipboardCheck, LogIn, LogOut, ReceiptText, UserCircle } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../components/common/PageHeader'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { useToast } from '../../hooks/useToast'
import { checkIn, checkOut, getEmployeeAttendance } from '../../services/attendanceService'
import { getLeaveRequests } from '../../services/leaveService'
import { getEmployeePayrollHistory } from '../../services/payrollService'
import { errorMessage } from '../../utils/errors'
import { enumLabel, formatAmount, formatTime, todayInput } from '../../utils/formatters'

/** Home page for Employees and Managers: today's attendance, own leave and payslips, and (Managers) leave to review. */
export default function EmployeeHome() {
  const { user, hasRole } = useAuth()
  const { notify } = useToast()
  const employeeId = user?.employeeId ?? ''
  const isManager = hasRole('Manager')
  const [busy, setBusy] = useState<'in' | 'out' | null>(null)

  const load = useCallback(
    (signal: AbortSignal) =>
      employeeId
        ? Promise.all([
            getEmployeeAttendance(employeeId, { date: todayInput() }, signal),
            getLeaveRequests({ scope: 'mine' }, signal),
            getEmployeePayrollHistory(employeeId, signal),
            isManager ? getLeaveRequests({ scope: 'team', status: 'Pending' }, signal) : Promise.resolve([]),
          ])
        : Promise.resolve(null),
    [employeeId, isManager],
  )
  const { data, reload } = useApi(load)
  const [today, leave, payroll, teamPending] = data ?? [[], [], [], []]
  const record = today[0]
  const latestPay = payroll.find((r) => r.periodStatus === 'Approved' || r.periodStatus === 'Finalized' || r.periodStatus === 'Paid')

  const run = async (kind: 'in' | 'out') => {
    setBusy(kind)
    try {
      const result = kind === 'in' ? await checkIn({ employeeId }) : await checkOut({ employeeId })
      notify('success', kind === 'in' ? `Checked in at ${formatTime(result.checkInTime)} (${enumLabel(result.status)}).` : `Checked out at ${formatTime(result.checkOutTime)}.`)
      reload()
    } catch (error) {
      notify('error', errorMessage(error))
    } finally {
      setBusy(null)
    }
  }

  return (
    <>
      <PageHeader title={`Welcome, ${user?.displayName ?? ''}`} description="Your day at a glance." />

      {!employeeId ? (
        <div role="note" className="alert alert-info alert-soft">
          <span className="text-sm">Your account is not linked to an employee record, so there is no personal data to show.</span>
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-2">
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-3">
              <h2 className="font-semibold">Today</h2>
              <p className="text-sm text-base-content/70">
                {record
                  ? `${enumLabel(record.status)} · in ${formatTime(record.checkInTime)} · out ${formatTime(record.checkOutTime)}`
                  : 'You have not checked in today.'}
              </p>
              <div className="flex gap-2">
                <button type="button" className="btn btn-primary btn-sm" disabled={busy !== null || !!record?.checkInTime} onClick={() => run('in')}>
                  {busy === 'in' ? <span className="loading loading-spinner loading-xs" /> : <LogIn className="size-4" />} Check in
                </button>
                <button type="button" className="btn btn-sm" disabled={busy !== null || !record?.checkInTime || !!record?.checkOutTime} onClick={() => run('out')}>
                  {busy === 'out' ? <span className="loading loading-spinner loading-xs" /> : <LogOut className="size-4" />} Check out
                </button>
              </div>
            </div>
          </div>

          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-2">
              <h2 className="font-semibold">Latest payslip</h2>
              {latestPay ? (
                <>
                  <p className="text-sm text-base-content/60">{latestPay.periodName}</p>
                  <p className="text-2xl font-bold tabular-nums text-primary">{formatAmount(latestPay.netSalary)}</p>
                  <Link to={`/payroll/payslip/${latestPay.id}`} className="link link-primary text-sm">
                    Open payslip
                  </Link>
                </>
              ) : (
                <p className="text-sm text-base-content/60">No approved payslip yet.</p>
              )}
            </div>
          </div>

          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-2">
              <h2 className="font-semibold">My leave</h2>
              <p className="text-sm text-base-content/70">
                {leave.filter((l) => l.status === 'Pending').length} pending · {leave.filter((l) => l.status === 'Approved').length} approved
              </p>
              <Link to="/leave" className="link link-primary text-sm">
                Apply or view requests
              </Link>
            </div>
          </div>

          {isManager && (
            <div className="card bg-base-100 shadow-sm">
              <div className="card-body gap-2">
                <h2 className="font-semibold">Team leave to review</h2>
                <p className="text-3xl font-semibold">{teamPending.length}</p>
                <Link to="/leave/approvals" className="link link-primary text-sm">
                  Review requests
                </Link>
              </div>
            </div>
          )}

          <div className="flex flex-wrap gap-2 lg:col-span-2">
            <Link to="/profile" className="btn btn-sm">
              <UserCircle className="size-4" /> My profile
            </Link>
            <Link to="/attendance/me" className="btn btn-sm">
              <CalendarDays className="size-4" /> My attendance
            </Link>
            <Link to="/payroll/my" className="btn btn-sm">
              <ReceiptText className="size-4" /> My payroll
            </Link>
            {isManager && (
              <Link to="/leave/approvals" className="btn btn-sm">
                <ClipboardCheck className="size-4" /> Leave approvals
              </Link>
            )}
          </div>
        </div>
      )}
    </>
  )
}
