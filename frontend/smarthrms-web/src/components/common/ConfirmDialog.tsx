import { useState } from 'react'
import { ApiErrorAlert } from './ApiErrorAlert'
import { Modal } from './Modal'

interface ConfirmDialogProps {
  open: boolean
  title: string
  message: string
  confirmLabel?: string
  tone?: 'danger' | 'primary'
  onCancel: () => void
  /** Runs the action. If it throws, the error is shown in the dialog and the dialog stays open. */
  onConfirm: () => Promise<void>
}

export function ConfirmDialog({ open, title, message, confirmLabel = 'Confirm', tone = 'danger', onCancel, onConfirm }: ConfirmDialogProps) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const close = () => {
    setError(null)
    onCancel()
  }

  const confirm = async () => {
    setBusy(true)
    setError(null)
    try {
      await onConfirm()
    } catch (caught) {
      setError(caught)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open={open} title={title} onClose={close} busy={busy}>
      <p className="text-sm text-base-content/80">{message}</p>
      {error !== null && (
        <div className="mt-4">
          <ApiErrorAlert error={error} />
        </div>
      )}
      <div className="modal-action">
        <button type="button" className="btn btn-ghost" onClick={close} disabled={busy}>
          Cancel
        </button>
        <button type="button" className={`btn ${tone === 'danger' ? 'btn-error' : 'btn-primary'}`} onClick={confirm} disabled={busy}>
          {busy && <span className="loading loading-spinner loading-sm" />}
          {confirmLabel}
        </button>
      </div>
    </Modal>
  )
}
