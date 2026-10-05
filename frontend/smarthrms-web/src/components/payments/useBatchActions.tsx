import { useState, type ReactNode } from 'react'
import { useToast } from '../../hooks/useToast'
import { cancelPaymentBatch, processPaymentBatch } from '../../services/paymentService'
import type { PaymentBatch } from '../../types/payment'
import { formatAmount } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { Modal } from '../common/Modal'

export type BatchAction = 'process' | 'cancel'

/**
 * Admin batch actions with a confirmation each (cancel asks for a reason). The server checks the batch state and
 * permission again; the dialog shows its error when it refuses. Render `dialogs` once in the page.
 */
export function useBatchActions(onDone: (batch: PaymentBatch) => void): { request: (action: BatchAction, batch: PaymentBatch) => void; dialogs: ReactNode } {
  const { notify } = useToast()
  const [pending, setPending] = useState<{ action: BatchAction; batch: PaymentBatch } | null>(null)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const close = () => {
    setPending(null)
    setReason('')
    setError(null)
  }

  const run = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!pending) return
    setBusy(true)
    setError(null)
    try {
      const { action, batch } = pending
      const updated = action === 'process' ? await processPaymentBatch(batch.id) : await cancelPaymentBatch(batch.id, reason.trim())
      notify('success', action === 'process' ? `Processing of ${batch.batchNumber} started.` : `${batch.batchNumber} cancelled.`)
      close()
      onDone(updated)
    } catch (caught) {
      setError(caught)
    } finally {
      setBusy(false)
    }
  }

  const batch = pending?.batch
  const cancel = pending?.action === 'cancel'
  const dialogs = pending && batch && (
    <Modal open title={cancel ? 'Cancel payment batch?' : 'Start payment processing?'} onClose={close} busy={busy}>
      <form onSubmit={run} className="flex flex-col gap-3">
        <p className="text-sm text-base-content/80">
          {cancel
            ? `Cancel ${batch.batchNumber}? Its ${batch.totalEmployees} pending payments are cancelled and the payroll can be paid in a new batch.`
            : `Move the ${batch.counts.pending} pending payments of ${batch.batchNumber} (${formatAmount(batch.totalAmount)}) to Processing. Each payment is then marked paid or failed individually.`}
        </p>
        {cancel && (
          <label className="fieldset py-0">
            <span className="fieldset-legend pb-1 text-sm font-medium">Reason</span>
            <textarea className="textarea w-full" rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} required />
          </label>
        )}
        {error !== null && <ApiErrorAlert error={error} />}
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={close} disabled={busy}>
            Close
          </button>
          <button type="submit" className={`btn ${cancel ? 'btn-error' : 'btn-primary'}`} disabled={busy || (cancel && !reason.trim())}>
            {busy && <span className="loading loading-spinner loading-sm" />}
            {cancel ? 'Cancel batch' : 'Start processing'}
          </button>
        </div>
      </form>
    </Modal>
  )

  return { request: (action, b) => setPending({ action, batch: b }), dialogs }
}
