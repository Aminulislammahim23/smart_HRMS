import { ReceiptText } from 'lucide-react'
import { useCallback, useState } from 'react'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { PayrollRecordModal } from '../../components/payroll/PayrollRecordModal'
import { PayrollRecordTable } from '../../components/payroll/PayrollRecordTable'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { getEmployeePayrollHistory } from '../../services/payrollService'
import type { PayrollRecord } from '../../types/payroll'
import { formatAmount } from '../../utils/formatters'

/** The signed-in employee's payroll history. The server returns only their own approved or paid payroll. */
export default function MyPayrollPage() {
  const { user } = useAuth()
  const employeeId = user?.employeeId ?? ''
  const [viewing, setViewing] = useState<PayrollRecord | null>(null)

  const load = useCallback((signal: AbortSignal) => getEmployeePayrollHistory(employeeId, signal), [employeeId])
  const { data: records, error, loading, reload } = useApi(load)
  // HR/Admin also see their own work-in-progress records; this page is about payslips that are final.
  const final = (records ?? []).filter((r) => r.periodStatus === 'Approved' || r.periodStatus === 'Paid')
  const latest = final[0]

  return (
    <>
      <PageHeader title="My payroll" description="Your approved salaries and payslips." />

      {error ? (
        <div className="card bg-base-100 shadow-sm">
          <ErrorState error={error} onRetry={reload} />
        </div>
      ) : loading && !records ? (
        <Loading label="Loading your payroll…" />
      ) : final.length === 0 ? (
        <div className="card bg-base-100 shadow-sm">
          <EmptyState icon={ReceiptText} title="No payslips yet" description="Your payslip appears here once HR's payroll for the month has been approved." />
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          {latest && (
            <div className="card bg-base-100 shadow-sm">
              <div className="card-body flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm text-base-content/60">Latest net salary · {latest.periodName}</p>
                  <p className="text-3xl font-bold tabular-nums text-primary">{formatAmount(latest.netSalary)}</p>
                  <p className="text-sm text-base-content/60">
                    Gross {formatAmount(latest.grossSalary)} · Deductions {formatAmount(latest.totalDeduction)}
                  </p>
                </div>
                <button type="button" className="btn btn-sm" onClick={() => setViewing(latest)}>
                  View breakdown
                </button>
              </div>
            </div>
          )}
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body gap-3">
              <h2 className="font-semibold">History</h2>
              <PayrollRecordTable records={final} onView={setViewing} showPeriod />
            </div>
          </div>
        </div>
      )}

      {viewing && <PayrollRecordModal record={viewing} onClose={() => setViewing(null)} />}
    </>
  )
}
