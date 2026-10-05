import { ArrowLeft, Printer } from 'lucide-react'
import { useCallback, useEffect, useRef } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PayslipView } from '../../components/payroll/PayslipView'
import { useApi } from '../../hooks/useApi'
import { getPayslip, getPayslipById } from '../../services/payrollService'

/**
 * A printable payslip, opened by payslip id (/payroll/payslips/:id) or, for the Day 16 links and HR previews, by
 * payroll record id (/payroll/payslip/:id). Employees reach only their own issued payslips; for anything else the
 * server answers "not found", which is what this page shows. ?print=1 opens the print dialog once loaded.
 */
export default function PayslipPage({ by = 'payslip' }: { by?: 'payslip' | 'record' }) {
  const { id = '' } = useParams()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const printed = useRef(false)

  const load = useCallback((signal: AbortSignal) => (by === 'payslip' ? getPayslipById(id, signal) : getPayslip(id, signal)), [by, id])
  const { data: payslip, error, loading, reload } = useApi(load)

  useEffect(() => {
    if (payslip && params.get('print') === '1' && !printed.current) {
      printed.current = true
      window.setTimeout(() => window.print(), 300)
    }
  }, [payslip, params])

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
        <button type="button" className="btn btn-primary btn-sm" onClick={() => window.print()} title="Use “Save as PDF” in the print dialog to download">
          <Printer className="size-4" /> Print / save as PDF
        </button>
      </div>
      <PayslipView payslip={payslip} />
    </div>
  )
}
