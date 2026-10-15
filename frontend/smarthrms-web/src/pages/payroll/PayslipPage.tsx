import { ArrowLeft, Printer } from 'lucide-react'
import { useCallback, useEffect, useRef } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PayslipPdfButton } from '../../components/payroll/PayslipPdfButton'
import { PayslipView } from '../../components/payroll/PayslipView'
import { useApi } from '../../hooks/useApi'
import { getPayslip, getPayslipById, recordPayslipPrint } from '../../services/payrollService'

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

  // Prints are recorded in the audit log (issued payslips only); a failed record never blocks printing.
  const print = useCallback(() => {
    if (payslip?.payslipId) recordPayslipPrint(payslip.payslipId).catch(() => undefined)
    window.print()
  }, [payslip])

  useEffect(() => {
    if (payslip && params.get('print') === '1' && !printed.current) {
      printed.current = true
      window.setTimeout(() => print(), 300)
    }
  }, [payslip, params, print])

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
        <div className="flex flex-wrap gap-2">
          {payslip.payslipId && <PayslipPdfButton payslipId={payslip.payslipId} payslipNumber={payslip.payslipNumber} label={`Download payslip ${payslip.payslipNumber} as PDF`} />}
          <button type="button" className="btn btn-primary btn-sm" onClick={print} title="Print, or use “Save as PDF” in the print dialog">
            <Printer className="size-4" /> Print / save as PDF
          </button>
        </div>
      </div>
      <PayslipView payslip={payslip} />
    </div>
  )
}
