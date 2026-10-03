import { AlertTriangle, ClipboardCheck, ListChecks, Pencil, UserX } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { Modal } from '../../components/common/Modal'
import { PageHeader } from '../../components/common/PageHeader'
import { PayrollPeriodForm } from '../../components/payroll/PayrollPeriodForm'
import { PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { PayrollSummaryCards } from '../../components/payroll/PayrollSummaryCards'
import { PayrollWorkflowButtons } from '../../components/payroll/PayrollWorkflowButtons'
import { usePayrollActions } from '../../components/payroll/usePayrollActions'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { getPayrollPeriod, updatePayrollPeriod } from '../../services/payrollService'
import type { PayrollCalculationResult, PayrollPeriod } from '../../types/payroll'
import { formatDate, formatDateTime } from '../../utils/formatters'

const STEPS: { label: string; at: (p: PayrollPeriod) => string | null; by: (p: PayrollPeriod) => string | null }[] = [
  { label: 'Created', at: (p) => p.createdAt, by: (p) => p.createdBy },
  { label: 'Calculated', at: (p) => p.calculatedAt, by: (p) => p.calculatedBy },
  { label: 'Submitted for approval', at: (p) => p.submittedAt, by: (p) => p.submittedBy },
  { label: 'Approved', at: (p) => p.approvedAt, by: (p) => p.approvedBy },
  { label: 'Paid', at: (p) => p.paidAt, by: (p) => p.paidBy },
]

/** One payroll period: totals, who did what and when, and the next workflow steps. */
export default function PayrollPeriodDetailsPage() {
  const { id = '' } = useParams()
  const { notify } = useToast()
  const [editing, setEditing] = useState(false)
  const [calculation, setCalculation] = useState<PayrollCalculationResult | null>(null)

  const load = useCallback((signal: AbortSignal) => getPayrollPeriod(id, signal), [id])
  const { data: period, error, loading, reload } = useApi(load)
  const { request, dialogs } = usePayrollActions((result) => {
    if (result) setCalculation(result)
    reload()
  })

  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} action={<Link to="/payroll/periods" className="btn btn-sm">Back to periods</Link>} /></div>
  if (loading && !period) return <Loading label="Loading payroll period…" />
  if (!period) return null

  const steps = period.status === 'Cancelled'
    ? [...STEPS.filter((step) => step.at(period)), { label: 'Cancelled', at: (p: PayrollPeriod) => p.cancelledAt, by: (p: PayrollPeriod) => p.cancelledBy }]
    : STEPS

  return (
    <>
      <PageHeader
        title={period.name}
        description={`${formatDate(period.startDate)} – ${formatDate(period.endDate)} · ${period.workingDays} working days`}
        actions={
          <>
            {period.actions.canEdit && (
              <button type="button" className="btn btn-sm" onClick={() => setEditing(true)}>
                <Pencil className="size-4" /> Edit
              </button>
            )}
            <Link to={`/payroll/${period.id}/records`} className="btn btn-sm">
              <ListChecks className="size-4" /> Records
            </Link>
            <Link to={`/payroll/${period.id}/approval`} className="btn btn-sm">
              <ClipboardCheck className="size-4" /> Approval
            </Link>
          </>
        }
      />

      <div className="flex flex-col gap-6">
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-sm text-base-content/60">Status</span>
              <PayrollStatusBadge status={period.status} />
              {period.payslipCount > 0 && (
                <Link
                  to={`/payroll/payslips?year=${period.startDate.slice(0, 4)}&month=${Number(period.startDate.slice(5, 7))}`}
                  className="badge badge-sm badge-soft badge-success"
                >
                  {period.payslipCount} payslips · {period.paidPayslipCount} paid
                </Link>
              )}
              {period.needsReviewCount > 0 && (
                <Link to={`/payroll/${period.id}/records?status=NeedsReview`} className="badge badge-sm badge-soft badge-error gap-1">
                  <AlertTriangle className="size-3" /> {period.needsReviewCount} need review
                </Link>
              )}
            </div>
            <PayrollWorkflowButtons period={period} onAction={(action) => request(action, period)} />
          </div>
        </div>

        {period.status === 'Draft' && (
          <div role="note" className="alert alert-info alert-soft">
            <span className="text-sm">
              This period has no payroll yet. Make sure employee <Link to="/payroll/salaries" className="link">salary structures</Link> are set, then
              calculate.
            </span>
          </div>
        )}

        <PayrollSummaryCards employees={period.employeeCount} gross={period.grossTotal} deduction={period.deductionTotal} net={period.netTotal} />

        {calculation && calculation.skipped.length > 0 && (
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-3">
              <div className="flex items-center gap-2">
                <UserX className="size-5 text-warning" />
                <h2 className="font-semibold">{calculation.skipped.length} employees were not included</h2>
              </div>
              <ul className="divide-y divide-base-200 text-sm">
                {calculation.skipped.map((s) => (
                  <li key={s.employeeId} className="flex flex-col gap-1 py-2 sm:flex-row sm:justify-between">
                    <span className="font-medium">
                      {s.employeeName} <span className="text-base-content/60">({s.employeeCode})</span>
                    </span>
                    <span className="text-base-content/70">{s.reason}</span>
                  </li>
                ))}
              </ul>
            </div>
          </div>
        )}

        <div className="grid gap-6 lg:grid-cols-2">
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body">
              <h2 className="font-semibold">Workflow</h2>
              <ul className="timeline timeline-vertical timeline-compact -ml-2">
                {steps.map((step, index) => {
                  const at = step.at(period)
                  return (
                    <li key={step.label}>
                      {index > 0 && <hr className={at ? 'bg-primary' : ''} />}
                      <div className="timeline-middle">
                        <span className={`block size-3 rounded-full ${at ? (step.label === 'Cancelled' ? 'bg-error' : 'bg-primary') : 'border border-base-300'}`} />
                      </div>
                      <div className="timeline-end mb-3">
                        <p className={`text-sm font-medium ${at ? '' : 'text-base-content/50'}`}>{step.label}</p>
                        <p className="text-xs text-base-content/60">{at ? `${formatDateTime(at)}${step.by(period) ? ` · ${step.by(period)}` : ''}` : 'Not yet'}</p>
                      </div>
                      {index < steps.length - 1 && <hr className={at ? 'bg-primary' : ''} />}
                    </li>
                  )
                })}
              </ul>
            </div>
          </div>
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body">
              <h2 className="font-semibold">Notes</h2>
              <p className="whitespace-pre-line text-sm text-base-content/70">{period.notes ?? 'No notes.'}</p>
              <h2 className="mt-4 font-semibold">How this payroll is calculated</h2>
              <ul className="list-disc space-y-1 pl-5 text-sm text-base-content/70">
                <li>Gross = basic + house rent + medical + transport + other allowance + overtime + bonus.</li>
                <li>Total deduction = tax + unpaid leave + advance + loan + other deduction.</li>
                <li>Net = gross − total deduction.</li>
                <li>Unpaid leave: monthly basic ÷ working days × approved unpaid-leave days. Paid leave is not deducted.</li>
                <li>Employees who joined during the period are paid for the working days they were employed.</li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      {editing && (
        <Modal open title={`Edit ${period.name}`} onClose={() => setEditing(false)} size="lg">
          <PayrollPeriodForm
            period={period}
            submitLabel="Save"
            onCancel={() => setEditing(false)}
            onSubmit={async (requestBody) => {
              await updatePayrollPeriod(period.id, requestBody)
              notify('success', 'Payroll period updated.')
              setEditing(false)
              reload()
            }}
          />
        </Modal>
      )}
      {dialogs}
    </>
  )
}
