import { useState } from 'react'
import { useToast } from '../../hooks/useToast'
import { cancelPayment, correctPayment, markPaymentFailed, markPaymentPaid, retryPayment } from '../../services/paymentService'
import { PAYMENT_METHODS, isPaymentMethod, requiresReference, type Payment, type PaymentMethod } from '../../types/payment'
import { blankToNull, enumLabel, formatAmount, formatDate, todayInput, toDateInput } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { Modal } from '../common/Modal'

export type PaymentAction = 'paid' | 'failed' | 'retry' | 'cancel' | 'correct'

const TEXT: Record<PaymentAction, { title: string; confirm: string; tone: string; reason: 'required' | 'optional' | null; done: string }> = {
  paid: { title: 'Confirm payment', confirm: 'Mark as paid', tone: 'btn-success', reason: null, done: 'marked as paid' },
  failed: { title: 'Mark payment as failed', confirm: 'Mark as failed', tone: 'btn-error', reason: 'required', done: 'marked as failed' },
  retry: { title: 'Retry payment', confirm: 'Retry', tone: 'btn-primary', reason: 'optional', done: 'moved back to processing' },
  cancel: { title: 'Cancel payment', confirm: 'Cancel payment', tone: 'btn-error', reason: 'required', done: 'cancelled' },
  correct: { title: 'Correct paid payment', confirm: 'Save correction', tone: 'btn-warning', reason: 'required', done: 'corrected' },
}

/** Allowed in a transaction reference (mirrors the server rule). Never enter PINs, OTPs or passwords. */
const REFERENCE = /^[A-Za-z0-9 ._/#:-]*$/

/**
 * Admin: one payment status change or a correction. The server checks the transition, the evidence rule and the
 * permission again, and records the history; this form only mirrors those rules to disable invalid submissions.
 */
export function PaymentActionModal({ payment, action, onClose, onDone }: { payment: Payment; action: PaymentAction; onClose: () => void; onDone: (updated: Payment) => void }) {
  const { notify } = useToast()
  const text = TEXT[action]
  const [reference, setReference] = useState('')
  const [date, setDate] = useState(action === 'correct' ? toDateInput(payment.paymentDate) : todayInput())
  const [reason, setReason] = useState('')
  const [remarks, setRemarks] = useState('')
  const [method, setMethod] = useState<PaymentMethod>(payment.paymentMethod)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const referenceInvalid = !REFERENCE.test(reference)
  const needsReference = action === 'paid' && requiresReference(payment.paymentMethod)
  const evidenceMissing = action === 'paid' && !reference.trim() && (needsReference || !remarks.trim())
  const correctionEmpty = action === 'correct' && !reference.trim() && date === toDateInput(payment.paymentDate)
  const blocked = referenceInvalid || evidenceMissing || correctionEmpty || (text.reason === 'required' && !reason.trim())

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const updated =
        action === 'paid'
          ? await markPaymentPaid(payment.id, { transactionReference: blankToNull(reference), paymentDate: date || null, remarks: blankToNull(remarks) })
          : action === 'failed'
            ? await markPaymentFailed(payment.id, { reason: reason.trim(), transactionReference: blankToNull(reference) })
            : action === 'retry'
              ? await retryPayment(payment.id, { reason: blankToNull(reason), paymentMethod: method === payment.paymentMethod ? null : method })
              : action === 'correct'
                ? await correctPayment(payment.id, {
                    transactionReference: blankToNull(reference),
                    paymentDate: date && date !== toDateInput(payment.paymentDate) ? date : null,
                    reason: reason.trim(),
                  })
                : await cancelPayment(payment.id, reason.trim())
      notify('success', `Payment of ${payment.employeeName} ${text.done}.`)
      onDone(updated)
    } catch (caught) {
      setError(caught)
      setBusy(false)
    }
  }

  const referenceLabel =
    action === 'failed'
      ? 'Attempt reference (optional)'
      : action === 'correct'
        ? `New transaction reference (current: ${payment.transactionReference ?? 'none'})`
        : needsReference
          ? 'Transaction reference (required)'
          : 'Reference, e.g. receipt number'

  return (
    <Modal open title={text.title} onClose={onClose} busy={busy}>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <p className="text-sm text-base-content/80">
          {payment.employeeName} ({payment.employeeCode}) · {payment.periodName} · {enumLabel(payment.paymentMethod)} ·{' '}
          <span className="font-medium tabular-nums">{formatAmount(payment.amount)}</span>
        </p>
        {action === 'paid' && (
          <p className="text-xs text-base-content/60">
            Record a payment only after the money has actually been transferred. The amount is the finalized net salary and can't be changed.
          </p>
        )}
        {action === 'correct' && (
          <p className="text-xs text-base-content/60">
            The payment stays paid and its amount can't change. Leave a field blank to keep it; the previous values stay in the payment history.
            Paid on {formatDate(payment.paymentDate)}.
          </p>
        )}
        {(action === 'paid' || action === 'failed' || action === 'correct') && (
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">{referenceLabel}</span>
            <input
              className={`input w-full ${referenceInvalid ? 'input-error' : ''}`}
              maxLength={100}
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder={action === 'failed' ? 'e.g. bank rejection ID' : 'e.g. bank or mobile transfer ID'}
              autoComplete="off"
              required={needsReference}
            />
            <span className={`text-xs ${referenceInvalid ? 'text-error' : 'text-base-content/60'}`}>
              Letters, digits, spaces and . _ / # : - only. Never enter PINs, OTPs, passwords or account numbers.
            </span>
          </label>
        )}
        {action === 'paid' && !needsReference && (
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Remarks (evidence, if there is no reference)</span>
            <textarea className="textarea w-full" rows={2} maxLength={400} value={remarks} onChange={(e) => setRemarks(e.target.value)} placeholder="e.g. paid in cash at the office, signed receipt kept" />
          </label>
        )}
        {(action === 'paid' || action === 'correct') && (
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Payment date</span>
            <input type="date" className="input w-full" value={date} max={todayInput()} onChange={(e) => setDate(e.target.value)} />
          </label>
        )}
        {action === 'retry' && (
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Payment method for the new attempt</span>
            <select className="select w-full" value={method} onChange={(e) => isPaymentMethod(e.target.value) && setMethod(e.target.value)}>
              {PAYMENT_METHODS.map((m) => (
                <option key={m} value={m}>
                  {enumLabel(m)}
                  {m === payment.paymentMethod ? ' (current)' : ''}
                </option>
              ))}
            </select>
          </label>
        )}
        {text.reason && (
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Reason{text.reason === 'optional' ? ' (optional)' : ''}</span>
            <textarea
              className="textarea w-full"
              rows={2}
              maxLength={action === 'correct' ? 300 : 500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              required={text.reason === 'required'}
            />
          </label>
        )}
        {error !== null && <ApiErrorAlert error={error} />}
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>
            Close
          </button>
          <button type="submit" className={`btn ${text.tone}`} disabled={busy || blocked}>
            {busy && <span className="loading loading-spinner loading-sm" />}
            {text.confirm}
          </button>
        </div>
      </form>
    </Modal>
  )
}
