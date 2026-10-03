import { ArrowLeft, Printer, Wallet } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { ApiErrorAlert } from '../../components/common/ApiErrorAlert'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PayslipView } from '../../components/payroll/PayslipView'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { useToast } from '../../hooks/useToast'
import { getPayslip, getPayslipById, recordPayslipPayment } from '../../services/payrollService'
import type { Payslip } from '../../types/payroll'
import { todayInput } from '../../utils/formatters'

/** Admin: record the salary payment of this payslip (the date defaults to today; the server refuses future dates). */
function RecordPayment({ payslip, onPaid }: { payslip: Payslip; onPaid: () => void }) {
  const { notify } = useToast()
  const [date, setDate] = useState(todayInput())
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const submit = async () => {
    if (!payslip.payslipId) return
    setBusy(true)
    setError(null)
    try {
      await recordPayslipPayment(payslip.payslipId, date)
      notify('success', `Payment of ${payslip.payslipNumber} recorded.`)
      onPaid()
    } catch (caught) {
      setError(caught)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card bg-base-100 shadow-sm print:hidden">
      <div className="card-body gap-3 p-4 sm:flex-row sm:items-end">
        <div className="flex-1">
          <h2 className="font-semibold">Record payment</h2>
          <p className="text-sm text-base-content/60">Marks this payslip as paid. The payroll becomes Paid once every payslip is paid.</p>
          {error !== null && <div className="mt-2"><ApiErrorAlert error={error} /></div>}
        </div>
        <label className="fieldset py-0">
          <span className="fieldset-legend pb-1 text-sm font-medium">Payment date</span>
          <input type="date" className="input input-sm" value={date} max={todayInput()} onChange={(e) => setDate(e.target.value)} />
        </label>
        <button type="button" className="btn btn-success btn-sm" onClick={submit} disabled={busy || !date}>
          {busy ? <span className="loading loading-spinner loading-xs" /> : <Wallet className="size-4" />} Mark as paid
        </button>
      </div>
    </div>
  )
}

/**
 * A printable payslip, opened by payslip id (/payroll/payslips/:id) or, for the Day 16 links and HR previews, by
 * payroll record id (/payroll/payslip/:id). Employees reach only their own issued payslips; for anything else the
 * server answers "not found", which is what this page shows. ?print=1 opens the print dialog once loaded.
 */
export default function PayslipPage({ by = 'payslip' }: { by?: 'payslip' | 'record' }) {
  const { id = '' } = useParams()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const { hasRole } = useAuth()
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

  const canRecordPayment = hasRole('Admin') && !!payslip.payslipId && payslip.paymentStatus === 'Unpaid' && payslip.payrollStatus === 'Approved'

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
      {canRecordPayment && <RecordPayment payslip={payslip} onPaid={reload} />}
      <PayslipView payslip={payslip} />
    </div>
  )
}
