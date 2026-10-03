import { ClipboardCheck, Plus } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { ApplyLeaveModal, LeaveDecisionModal, LeaveTable, type LeaveDecision } from '../../components/leave/LeaveComponents'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { usePagination } from '../../hooks/usePagination'
import { getEmployees } from '../../services/employeeService'
import { getLeaveRequests } from '../../services/leaveService'
import { LEAVE_STATUSES, LEAVE_TYPES, isLeaveStatus, isLeaveType, type LeaveRequest } from '../../types/leave'
import { enumLabel } from '../../utils/formatters'

/**
 * Leave to review. Managers see their direct reports; HR and Admin see everyone. Defaults to pending requests.
 * Nobody can approve their own leave: the server refuses it and the buttons don't appear.
 */
export default function LeaveApprovalsPage() {
  const { hasRole } = useAuth()
  const isHr = hasRole('HR', 'Admin')
  const [params, setParams] = useSearchParams()
  const statusParam = params.get('status') ?? 'Pending'
  const status = isLeaveStatus(statusParam) ? statusParam : undefined
  const typeParam = params.get('type') ?? ''
  const leaveType = isLeaveType(typeParam) ? typeParam : undefined
  const [deciding, setDeciding] = useState<{ request: LeaveRequest; decision: LeaveDecision } | null>(null)
  const [applying, setApplying] = useState(false)

  const setFilter = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    next.set(key, value)
    setParams(next, { replace: true })
  }

  const load = useCallback(
    (signal: AbortSignal) => getLeaveRequests({ scope: isHr ? 'all' : 'team', status, leaveType }, signal),
    [isHr, status, leaveType],
  )
  const { data: requests, error, loading, reload } = useApi(load)
  const loadEmployees = useCallback((signal: AbortSignal) => (isHr ? getEmployees(signal) : Promise.resolve([])), [isHr])
  const { data: employees } = useApi(loadEmployees)
  const rows = useMemo(() => requests ?? [], [requests])
  const pagination = usePagination(rows, 25)

  return (
    <>
      <PageHeader
        title="Leave approvals"
        description={isHr ? 'Leave requests of all employees.' : 'Leave requests of your direct reports.'}
        actions={
          isHr && (
            <button type="button" className="btn btn-sm" onClick={() => setApplying(true)}>
              <Plus className="size-4" /> Record leave for an employee
            </button>
          )
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="flex flex-col gap-3 sm:flex-row">
            <select className="select select-sm w-full sm:w-44" value={status ?? 'all'} onChange={(e) => setFilter('status', e.target.value)} aria-label="Status">
              <option value="all">All statuses</option>
              {LEAVE_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
            <select className="select select-sm w-full sm:w-44" value={leaveType ?? ''} onChange={(e) => setFilter('type', e.target.value)} aria-label="Leave type">
              <option value="">All types</option>
              {LEAVE_TYPES.map((t) => (
                <option key={t} value={t}>
                  {enumLabel(t)}
                </option>
              ))}
            </select>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !requests ? (
            <Loading label="Loading leave requests…" />
          ) : rows.length === 0 ? (
            <EmptyState icon={ClipboardCheck} title={status === 'Pending' ? 'Nothing waiting for review' : 'No leave requests match'} />
          ) : (
            <>
              <LeaveTable requests={pagination.pageItems} showEmployee onDecide={(request, decision) => setDeciding({ request, decision })} />
              <Pagination
                page={pagination.page}
                pageCount={pagination.pageCount}
                pageSize={pagination.pageSize}
                total={pagination.total}
                onPageChange={pagination.setPage}
                onPageSizeChange={pagination.setPageSize}
              />
            </>
          )}
        </div>
      </div>

      {deciding && <LeaveDecisionModal request={deciding.request} decision={deciding.decision} onClose={() => setDeciding(null)} onDone={reload} />}
      {applying && <ApplyLeaveModal employees={(employees ?? []).filter((e) => e.isActive)} onClose={() => setApplying(false)} onDone={reload} />}
    </>
  )
}
