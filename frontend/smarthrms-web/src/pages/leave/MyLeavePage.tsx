import { CalendarDays, Plus } from 'lucide-react'
import { useCallback, useState } from 'react'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { ApplyLeaveModal, LeaveDecisionModal, LeaveTable, type LeaveDecision } from '../../components/leave/LeaveComponents'
import { useApi } from '../../hooks/useApi'
import { getLeaveRequests } from '../../services/leaveService'
import type { LeaveRequest } from '../../types/leave'

/** The signed-in employee's own leave: apply, follow the status, cancel while pending. */
export default function MyLeavePage() {
  const [applying, setApplying] = useState(false)
  const [deciding, setDeciding] = useState<{ request: LeaveRequest; decision: LeaveDecision } | null>(null)
  const load = useCallback((signal: AbortSignal) => getLeaveRequests({ scope: 'mine' }, signal), [])
  const { data: requests, error, loading, reload } = useApi(load)

  const year = new Date().getFullYear()
  const thisYear = (requests ?? []).filter((r) => r.status === 'Approved' && r.startDate.startsWith(String(year)))
  const stats = [
    { label: `Approved days ${year}`, value: thisYear.reduce((sum, r) => sum + r.totalDays, 0) },
    { label: 'Unpaid days', value: thisYear.filter((r) => !r.isPaid).reduce((sum, r) => sum + r.totalDays, 0) },
    { label: 'Pending requests', value: (requests ?? []).filter((r) => r.status === 'Pending').length },
  ]

  return (
    <>
      <PageHeader
        title="My leave"
        description="Apply for leave and follow your requests. Weekends are not counted."
        actions={
          <button type="button" className="btn btn-primary btn-sm" onClick={() => setApplying(true)}>
            <Plus className="size-4" /> Apply for leave
          </button>
        }
      />

      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        {stats.map(({ label, value }) => (
          <div key={label} className="card bg-base-100 shadow-sm">
            <div className="card-body p-5">
              <p className="text-sm text-base-content/60">{label}</p>
              <p className="text-2xl font-semibold tabular-nums">{value}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-3 p-4 sm:p-6">
          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !requests ? (
            <Loading label="Loading your leave…" />
          ) : !requests?.length ? (
            <EmptyState
              icon={CalendarDays}
              title="No leave requests yet"
              action={
                <button type="button" className="btn btn-primary btn-sm" onClick={() => setApplying(true)}>
                  <Plus className="size-4" /> Apply for leave
                </button>
              }
            />
          ) : (
            <LeaveTable requests={requests} onDecide={(request, decision) => setDeciding({ request, decision })} />
          )}
        </div>
      </div>

      {applying && <ApplyLeaveModal onClose={() => setApplying(false)} onDone={reload} />}
      {deciding && <LeaveDecisionModal request={deciding.request} decision={deciding.decision} onClose={() => setDeciding(null)} onDone={reload} />}
    </>
  )
}
