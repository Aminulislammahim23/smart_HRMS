import { useState } from 'react'
import { useToast } from '../../hooks/useToast'
import { cancelPayment, markPaymentFailed, markPaymentPaid, retryPayment } from '../../services/paymentService'
import type { Payment } from '../../types/payment'
import { blankToNull, formatAmount, todayInput } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { Modal } from '../common/Modal'

export type PaymentAction = 'paid' | 'failed' | 'retry' | 'cancel'

const TEXT: Record<PaymentAction, { title: string; confirm: string; tone: string; reason: 'required' | 'optional' | null; done: string }> = {
  paid: { title: 'Mark payment as paid', confirm: 'Mark as paid', tone: 'btn-success', reason: null, done: 'marked as paid' },
  failed: { title: 'Mark payment as failed', confirm: 'Mark as failed', tone: 'btn-error', reason: 'required', done: 'marked as failed' },
  retry: { title: 'Retry payment', confirm: 'Retry', tone: 'btn-primary', reason: 'optional', done: 'moved back to processing' },
  cancel: { title: 'Cancel payment', confirm: 'Cancel payment', tone: 'btn-error', reason: 'required', done: 'cancelled' },
}

/** Allowed in a transaction reference (mirrors the server rule). Never enter PINs, OTPs or passwords. */
const REFERENCE = /^[A-Za-z0-9 ._/#:-]*$/

/** Admin: one payment status change. The server checks the transition and permission again and records history. */
export function PaymentActionModal({ payment, action, onClose, onDone }: { payment: Payment; action: PaymentAction; onClose: () => void; onDone: (updated: Payment) => void }) {
  const { notify } = useToast()
  const text = TEXT[action]
  const [reference, setReference] = useState('')
  const [date, setDate] = useState(todayInput())
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const referenceInvalid = !REFERENCE.test(reference)

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const updated =
        action === 'paid'
          ? await markPaymentPaid(payment.id, { transactionReference: blankToNull(reference), paymentDate: date || null })
          : action === 'failed'
            ? await markPaymentFailed(payment.id, reason.trim())
            : action === 'retry'
              ? await retryPayment(payment.id, blankToNull(reason))
              : await cancelPayment(payment.id, reason.trim())
      notify('success', `Payment of ${payment.employeeName} ${text.done}.`)
      onDone(updated)
    } catch (caught) {
      setError(caught)
      setBusy(false)
    }
  }

  return (
    <Modal open title={text.title} onClose={onClose} busy={busy}>
      <form onSubmit={submit} className="flex flex-col gap-3">
        <p className="text-sm text-base-content/80">
          {payment.employeeName} ({payment.employeeCode}) · {payment.periodName} · <span className="font-medium tabular-nums">{formatAmount(payment.amount)}</span>
        </p>
        {action === 'paid' && (
          <>
            <label className="fieldset py-0">
              <span className="fieldset-legend pb-1 text-sm font-medium">Transaction reference (optional)</span>
              <input
                className={`input w-full ${referenceInvalid ? 'input-error' : ''}`}
                maxLength={100}
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                placeholder="e.g. bank or mobile transfer ID"
                autoComplete="off"
              />
              <span className={`text-xs ${referenceInvalid ? 'text-error' : 'text-base-content/60'}`}>
                Letters, digits, spaces and . _ / # : - only. Never enter PINs, OTPs or passwords.
              </span>
            </label>
            <label className="fieldset py-0">
              <span className="fieldset-legend pb-1 text-sm font-medium">Payment date</span>
              <input type="date" className="input w-full" value={date} max={todayInput()} onChange={(e) => setDate(e.target.value)} />
            </label>
          </>
        )}
        {text.reason && (
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Reason{text.reason === 'optional' ? ' (optional)' : ''}</span>
            <textarea className="textarea w-full" rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} required={text.reason === 'required'} />
          </label>
        )}
        {error !== null && <ApiErrorAlert error={error} />}
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>
            Close
          </button>
          <button type="submit" className={`btn ${text.tone}`} disabled={busy || referenceInvalid || (text.reason === 'required' && !reason.trim())}>
            {busy && <span className="loading loading-spinner loading-sm" />}
            {text.confirm}
          </button>
        </div>
      </form>
    </Modal>
  )
}
