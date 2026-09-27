import { Save } from 'lucide-react'

interface FormActionsProps {
  submitting: boolean
  onCancel: () => void
  submitLabel?: string
}

/** Cancel + submit buttons for forms shown in a modal. */
export function FormActions({ submitting, onCancel, submitLabel = 'Save' }: FormActionsProps) {
  return (
    <div className="modal-action">
      <button type="button" className="btn btn-ghost" onClick={onCancel} disabled={submitting}>
        Cancel
      </button>
      <button type="submit" className="btn btn-primary" disabled={submitting}>
        {submitting ? <span className="loading loading-spinner loading-sm" /> : <Save className="size-4" />}
        {submitLabel}
      </button>
    </div>
  )
}
