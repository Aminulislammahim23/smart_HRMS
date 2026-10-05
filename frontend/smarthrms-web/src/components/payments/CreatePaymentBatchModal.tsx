import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useToast } from '../../hooks/useToast'
import { createPaymentBatch } from '../../services/paymentService'
import { PAYMENT_METHODS, isPaymentMethod, type PaymentMethod } from '../../types/payment'
import type { PayrollPeriod } from '../../types/payroll'
import { blankToNull, enumLabel, formatAmount, todayInput } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { Modal } from '../common/Modal'

/**
 * Admin: pays every unpaid payslip of a finalized payroll in one batch. Only the method, planned date and notes are
 * sent; the server picks the employees and calculates the amounts and totals.
 */
export function CreatePaymentBatchModal({ period, onClose }: { period: PayrollPeriod; onClose: () => void }) {
  const { notify } = useToast()
  const navigate = useNavigate()
  const [method, setMethod] = useState<PaymentMethod>('BankTransfer')
  const [date, setDate] = useState(todayInput())
  const [notes, setNotes] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const unpaid = period.payslipCount - period.paidPayslipCount

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const batch = await createPaymentBatch({ payrollPeriodId: period.id, paymentMethod: method, paymentDate: date || null, notes: blankToNull(notes) })
      notify('success', `Payment batch ${batch.batchNumber} created for ${batch.totalEmployees} employees.`)
      navigate(`/payments/batches/${batch.id}`)
    } catch (caught) {
      setError(caught)
      setBusy(false)
    }
  }

  return (
    <Modal open title="Create payment batch" onClose={onClose} busy={busy}>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <p className="text-sm text-base-content/80">
          Pay the {unpaid} unpaid payslip{unpaid === 1 ? '' : 's'} of <span className="font-medium">{period.name}</span> (net payroll {formatAmount(period.netTotal)}).
          Employees with a zero net salary are not included. Amounts are taken from the finalized payroll.
        </p>
        <label className="fieldset py-0">
          <span className="fieldset-legend pb-1 text-sm font-medium">Payment method</span>
          <select className="select w-full" value={method} onChange={(e) => isPaymentMethod(e.target.value) && setMethod(e.target.value)} required>
            {PAYMENT_METHODS.map((m) => (
              <option key={m} value={m}>
                {enumLabel(m)}
              </option>
            ))}
          </select>
        </label>
        <label className="fieldset py-0">
          <span className="fieldset-legend pb-1 text-sm font-medium">Planned payment date</span>
          <input type="date" className="input w-full" value={date} min={period.startDate.slice(0, 10)} onChange={(e) => setDate(e.target.value)} />
        </label>
        <label className="fieldset py-0">
          <span className="fieldset-legend pb-1 text-sm font-medium">Notes (optional)</span>
          <textarea className="textarea w-full" rows={2} maxLength={500} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </label>
        {error !== null && <ApiErrorAlert error={error} />}
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            {busy && <span className="loading loading-spinner loading-sm" />}
            Create batch
          </button>
        </div>
      </form>
    </Modal>
  )
}
