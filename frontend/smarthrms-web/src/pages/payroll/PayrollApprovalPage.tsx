import { AlertTriangle, CheckCircle2, Info } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { PayrollRecordModal } from '../../components/payroll/PayrollRecordModal'
import { PayrollRecordTable } from '../../components/payroll/PayrollRecordTable'
import { PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { PayrollSummaryCards } from '../../components/payroll/PayrollSummaryCards'
import { PayrollWorkflowButtons } from '../../components/payroll/PayrollWorkflowButtons'
import { usePayrollActions } from '../../components/payroll/usePayrollActions'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { getPayrollPeriod, getPayrollRecords } from '../../services/payrollService'
import type { PayrollRecord } from '../../types/payroll'
import { formatAmount, formatDateTime } from '../../utils/formatters'

/**
 * The reviewer's view before approval: totals, the largest net salaries, records needing attention and the approval
 * buttons the server allows for this user (HR submits; Admin approves and marks paid).
 */
export default function PayrollApprovalPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const [viewing, setViewing] = useState<PayrollRecord | null>(null)

  const load = useCallback((signal: AbortSignal) => Promise.all([getPayrollPeriod(id, signal), getPayrollRecords(id, {}, signal)]), [id])
  const { data, error, loading, reload } = useApi(load)
  const { request, dialogs } = usePayrollActions(() => reload())
  const [period, records] = data ?? [undefined, []]

  const needsReview = useMemo(() => records.filter((r) => r.status === 'NeedsReview'), [records])
  const top = useMemo(() => [...records].sort((a, b) => b.netSalary - a.netSalary).slice(0, 5), [records])
  const includesOwnSalary = !!user?.employeeId && records.some((r) => r.employeeId === user.employeeId)

  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} /></div>
  if (loading && !data) return <Loading label="Loading payroll for approval…" />
  if (!period) return null

  return (
    <>
      <PageHeader
        title={`Approve ${period.name}`}
        description="Review the totals and exceptions before approving."
        actions={
          <Link to={`/payroll/${period.id}/records`} className="btn btn-sm">
            All records
          </Link>
        }
      />

      <div className="flex flex-col gap-6">
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div className="flex flex-wrap items-center gap-2">
              <PayrollStatusBadge status={period.status} />
              <span className="text-sm text-base-content/60">
                {period.submittedAt ? `Submitted ${formatDateTime(period.submittedAt)} by ${period.submittedBy ?? '—'}` : 'Not submitted yet'}
                {period.approvedAt && ` · Approved ${formatDateTime(period.approvedAt)} by ${period.approvedBy ?? '—'}`}
              </span>
            </div>
            <PayrollWorkflowButtons period={period} onAction={(action) => request(action, period)} />
          </div>
        </div>

        {period.status === 'PendingApproval' && !period.actions.canApprove && (
          <div role="note" className="alert alert-info alert-soft">
            <Info className="size-5 shrink-0" />
            <span className="text-sm">Waiting for an Admin to approve.</span>
          </div>
        )}
        {period.status === 'PendingApproval' && includesOwnSalary && period.actions.canApprove && (
          <div role="note" className="alert alert-warning alert-soft">
            <AlertTriangle className="size-5 shrink-0" />
            <span className="text-sm">This payroll includes your own salary, so the server will refuse your approval. Another Admin must approve it.</span>
          </div>
        )}

        <PayrollSummaryCards employees={period.employeeCount} gross={period.grossTotal} deduction={period.deductionTotal} net={period.netTotal} />

        <div className="card bg-base-100 shadow-sm">
          <div className="card-body gap-3">
            <h2 className="font-semibold">Checks</h2>
            <ul className="flex flex-col gap-2 text-sm">
              <Check ok={records.length > 0} text={records.length > 0 ? `${records.length} employees have payroll records.` : 'No payroll records: calculate the payroll first.'} />
              <Check ok={needsReview.length === 0} text={needsReview.length === 0 ? 'No record needs review.' : `${needsReview.length} records need review (negative net salary).`} />
              <Check ok={records.every((r) => r.netSalary >= 0)} text={`Net total ${formatAmount(period.netTotal)} for ${period.workingDays} working days.`} />
            </ul>
          </div>
        </div>

        {needsReview.length > 0 && (
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-3">
              <h2 className="font-semibold text-error">Records needing review</h2>
              <PayrollRecordTable records={needsReview} onView={setViewing} />
            </div>
          </div>
        )}

        <div className="card bg-base-100 shadow-sm">
          <div className="card-body gap-3">
            <h2 className="font-semibold">Highest net salaries</h2>
            {top.length === 0 ? <p className="text-sm text-base-content/60">No records.</p> : <PayrollRecordTable records={top} onView={setViewing} />}
          </div>
        </div>
      </div>

      {viewing && <PayrollRecordModal record={viewing} onClose={() => setViewing(null)} />}
      {dialogs}
    </>
  )
}

function Check({ ok, text }: { ok: boolean; text: string }) {
  return (
    <li className="flex items-start gap-2">
      {ok ? <CheckCircle2 className="size-4 shrink-0 text-success" /> : <AlertTriangle className="size-4 shrink-0 text-error" />}
      <span>{text}</span>
    </li>
  )
}
