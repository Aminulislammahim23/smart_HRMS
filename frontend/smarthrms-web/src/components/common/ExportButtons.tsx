import { FileSpreadsheet, FileText } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '../../hooks/useToast'
import type { ExportFormat } from '../../types/payrollReport'
import { errorMessage } from '../../utils/errors'

/**
 * CSV and Excel download buttons. `onExport` calls the server, which applies the same filters and permissions as the
 * list on screen; a refusal (e.g. too many rows) is shown as a toast.
 */
export function ExportButtons({ onExport, disabled = false, label = 'Export' }: { onExport: (format: ExportFormat) => Promise<void>; disabled?: boolean; label?: string }) {
  const { notify } = useToast()
  const [busy, setBusy] = useState<ExportFormat | null>(null)

  const run = async (format: ExportFormat) => {
    setBusy(format)
    try {
      await onExport(format)
    } catch (error) {
      notify('error', errorMessage(error))
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="join" role="group" aria-label={label}>
      <button type="button" className="btn btn-sm join-item" onClick={() => run('csv')} disabled={disabled || busy !== null} title={`${label} as CSV`}>
        {busy === 'csv' ? <span className="loading loading-spinner loading-xs" /> : <FileText className="size-4" />} CSV
      </button>
      <button type="button" className="btn btn-sm join-item" onClick={() => run('xlsx')} disabled={disabled || busy !== null} title={`${label} as Excel`}>
        {busy === 'xlsx' ? <span className="loading loading-spinner loading-xs" /> : <FileSpreadsheet className="size-4" />} Excel
      </button>
    </div>
  )
}
