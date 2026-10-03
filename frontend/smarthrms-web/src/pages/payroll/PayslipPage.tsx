import { ArrowLeft, Printer } from 'lucide-react'
import { useCallback } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PayslipView } from '../../components/payroll/PayslipView'
import { useApi } from '../../hooks/useApi'
import { getPayslip } from '../../services/payrollService'

/**
 * A printable payslip. Employees reach only their own approved or paid payslips; for anything else the server answers
 * "not found", which is what this page shows.
 */
export default function PayslipPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const load = useCallback((signal: AbortSignal) => getPayslip(id, signal), [id])
  const { data: payslip, error, loading, reload } = useApi(load)

  if (error) {
    return (
      <div className="card bg-base-100 shadow-sm">
        <ErrorState error={error} onRetry={reload} action={<Link to="/payroll/my" className="btn btn-sm">My payroll</Link>} />
      </div>
    )
  }
  if (loading && !payslip) return <Loading label="Loading payslip…" />
  if (!payslip) return null

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2 print:hidden">
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => navigate(-1)}>
          <ArrowLeft className="size-4" /> Back
        </button>
        <button type="button" className="btn btn-primary btn-sm" onClick={() => window.print()}>
          <Printer className="size-4" /> Print payslip
        </button>
      </div>
      <PayslipView payslip={payslip} />
    </div>
  )
}
