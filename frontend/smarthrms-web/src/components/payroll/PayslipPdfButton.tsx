import { FileDown } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '../../hooks/useToast'
import { downloadPayslipPdf } from '../../services/payrollService'
import { errorMessage } from '../../utils/errors'

/**
 * Downloads a payslip as PDF. The server decides access (own finalized payslip, or HR/Admin) and audits the download;
 * a refusal is shown as a toast.
 */
export function PayslipPdfButton({ payslipId, payslipNumber, label, compact = false }: { payslipId: string; payslipNumber: string | null; label: string; compact?: boolean }) {
  const { notify } = useToast()
  const [busy, setBusy] = useState(false)

  const download = async () => {
    setBusy(true)
    try {
      await downloadPayslipPdf(payslipId, payslipNumber)
    } catch (error) {
      notify('error', errorMessage(error))
    } finally {
      setBusy(false)
    }
  }

  const icon = busy ? <span className="loading loading-spinner loading-xs" /> : <FileDown className="size-4" />
  return compact ? (
    <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={download} disabled={busy} title="Download PDF" aria-label={label}>
      {icon}
    </button>
  ) : (
    <button type="button" className="btn btn-sm" onClick={download} disabled={busy} aria-label={label}>
      {icon} Download PDF
    </button>
  )
}
