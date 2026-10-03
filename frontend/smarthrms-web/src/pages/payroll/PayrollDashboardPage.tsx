import { Landmark, Plus } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { PayrollPeriodTable } from '../../components/payroll/PayrollPeriodTable'
import { PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { PayrollSummaryCards } from '../../components/payroll/PayrollSummaryCards'
import { PayrollWorkflowButtons } from '../../components/payroll/PayrollWorkflowButtons'
import { usePayrollActions } from '../../components/payroll/usePayrollActions'
import { useApi } from '../../hooks/useApi'
import { getPayrollPeriods } from '../../services/payrollService'
import { PAYROLL_STATUSES, isPayrollStatus } from '../../types/payroll'
import { enumLabel, formatDate } from '../../utils/formatters'

const CURRENT_YEAR = new Date().getFullYear()

/**
 * HR overview. The summary cards show the selected payroll period (by default the latest that isn't cancelled); the
 * counters below cover every period of the year.
 */
export default function PayrollDashboardPage() {
  const [year, setYear] = useState(CURRENT_YEAR)
  const [status, setStatus] = useState('')
  const [selectedId, setSelectedId] = useState('')

  const load = useCallback((signal: AbortSignal) => getPayrollPeriods({ year }, signal), [year])
  const { data: periods, error, loading, reload } = useApi(load)
  const { request, dialogs } = usePayrollActions(() => reload())

  const filtered = useMemo(() => (periods ?? []).filter((p) => !status || p.status === status), [periods, status])
  const selected = filtered.find((p) => p.id === selectedId) ?? filtered.find((p) => p.status !== 'Cancelled') ?? filtered[0]

  return (
    <>
      <PageHeader
        title="Payroll"
        description="Salary processing: calculate, review, approve and pay."
        actions={
          <Link to="/payroll/create" className="btn btn-primary btn-sm">
            <Plus className="size-4" /> New payroll period
          </Link>
        }
      />

      <div className="card mb-6 bg-base-100 shadow-sm">
        <div className="card-body flex-col gap-3 p-4 md:flex-row md:items-end">
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Year</span>
            <select className="select select-sm w-full md:w-32" value={year} onChange={(e) => { setYear(Number(e.target.value)); setSelectedId('') }}>
              {Array.from({ length: 6 }, (_, i) => CURRENT_YEAR + 1 - i).map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
          </label>
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Status</span>
            <select className="select select-sm w-full md:w-44" value={status} onChange={(e) => { setStatus(isPayrollStatus(e.target.value) ? e.target.value : ''); setSelectedId('') }}>
              <option value="">All statuses</option>
              {PAYROLL_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {enumLabel(s)}
                </option>
              ))}
            </select>
          </label>
          <label className="fieldset py-0 md:flex-1">
            <span className="fieldset-legend pb-1 text-sm font-medium">Payroll period</span>
            <select className="select select-sm w-full" value={selected?.id ?? ''} onChange={(e) => setSelectedId(e.target.value)} disabled={filtered.length === 0}>
              {filtered.length === 0 && <option value="">No periods</option>}
              {filtered.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} — {enumLabel(p.status)}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {error ? (
        <div className="card bg-base-100 shadow-sm">
          <ErrorState error={error} onRetry={reload} />
        </div>
      ) : loading && !periods ? (
        <Loading label="Loading payroll…" />
      ) : !selected ? (
        <div className="card bg-base-100 shadow-sm">
          <EmptyState
            icon={Landmark}
            title={periods?.length ? 'No payroll periods match this status' : `No payroll periods in ${year}`}
            description="Create a payroll period, set employee salaries, then calculate the payroll."
            action={
              <Link to="/payroll/create" className="btn btn-primary btn-sm">
                <Plus className="size-4" /> New payroll period
              </Link>
            }
          />
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          <PayrollSummaryCards
            employees={selected.employeeCount}
            gross={selected.grossTotal}
            deduction={selected.deductionTotal}
            net={selected.netTotal}
            pendingApproval={(periods ?? []).filter((p) => p.status === 'PendingApproval').length}
            paid={(periods ?? []).filter((p) => p.status === 'Paid').length}
            hint={selected.name}
          />

          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-4">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-lg font-semibold">{selected.name}</h2>
                    <PayrollStatusBadge status={selected.status} />
                  </div>
                  <p className="text-sm text-base-content/60">
                    {formatDate(selected.startDate)} – {formatDate(selected.endDate)} · {selected.workingDays} working days
                    {selected.needsReviewCount > 0 && <span className="text-error"> · {selected.needsReviewCount} need review</span>}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Link to={`/payroll/${selected.id}/records`} className="btn btn-sm">
                    View records
                  </Link>
                  <Link to={`/payroll/${selected.id}`} className="btn btn-sm">
                    Details
                  </Link>
                  <PayrollWorkflowButtons period={selected} onAction={(action) => request(action, selected)} />
                </div>
              </div>
            </div>
          </div>

          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-3">
              <div className="flex items-center justify-between">
                <h2 className="font-semibold">Payroll periods in {year}</h2>
                <Link to="/payroll/periods" className="link link-primary text-sm">
                  All periods
                </Link>
              </div>
              <PayrollPeriodTable periods={filtered} />
            </div>
          </div>
        </div>
      )}
      {dialogs}
    </>
  )
}
