import { useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { useToast } from '../../hooks/useToast'
import { approvePayroll, calculatePayroll, cancelPayroll, deletePayrollPeriod, finalizePayroll, generatePayslips, submitPayroll } from '../../services/payrollService'
import type { PayrollCalculationResult, PayrollPeriod } from '../../types/payroll'
import { formatAmount } from '../../utils/formatters'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { CreatePaymentBatchModal } from '../payments/CreatePaymentBatchModal'

export type PayrollAction = 'calculate' | 'submit' | 'approve' | 'finalize' | 'createBatch' | 'cancel' | 'delete' | 'generatePayslips'

interface ActionText {
  title: string
  message: (period: PayrollPeriod) => string
  confirmLabel: string
  tone: 'primary' | 'danger'
}

const TEXT: Record<PayrollAction, ActionText> = {
  calculate: {
    title: 'Calculate payroll?',
    message: (p) =>
      `Calculate ${p.name} for every current employee from their salary structure, attendance and approved leave.` +
      (p.status === 'Calculated' ? ' Existing records are refreshed; overtime, bonus and manual deductions are kept.' : ''),
    confirmLabel: 'Calculate',
    tone: 'primary',
  },
  submit: {
    title: 'Submit for approval?',
    message: (p) => `Submit ${p.name} (${p.employeeCount} employees, net ${formatAmount(p.netTotal)}) for approval. Records can't be edited while it waits.`,
    confirmLabel: 'Submit',
    tone: 'primary',
  },
  approve: {
    title: 'Approve payroll?',
    message: (p) => `Approve ${p.name}: ${p.employeeCount} employees, net total ${formatAmount(p.netTotal)}. Payslips are issued to every employee at approval. Approved payroll can't be edited, recalculated or cancelled.`,
    confirmLabel: 'Approve',
    tone: 'primary',
  },
  finalize: {
    title: 'Finalize Payroll?',
    message: () => 'After finalization, this payroll will be locked and normal payroll values can no longer be edited. Are you sure you want to continue?',
    confirmLabel: 'Finalize Payroll',
    tone: 'primary',
  },
  // Not a confirmation: createBatch opens the payment batch form instead.
  createBatch: {
    title: 'Create payment batch',
    message: () => '',
    confirmLabel: 'Create batch',
    tone: 'primary',
  },
  cancel: {
    title: 'Cancel payroll?',
    message: (p) => `Cancel ${p.name}? Its records are kept for history but it can't be processed any more. A new period with the same dates can be created afterwards.`,
    confirmLabel: 'Cancel payroll',
    tone: 'danger',
  },
  generatePayslips: {
    title: 'Generate payslips?',
    message: (p) => `Issue the missing payslips of ${p.name} from its approved payroll records.`,
    confirmLabel: 'Generate',
    tone: 'primary',
  },
  delete: {
    title: 'Delete payroll period?',
    message: (p) => `Delete the draft period ${p.name}? This can't be undone.`,
    confirmLabel: 'Delete',
    tone: 'danger',
  },
}

/**
 * Payroll workflow actions with a confirmation dialog each. The server checks every transition and permission
 * again; the dialog shows its error when it refuses. Render `dialogs` once in the page.
 */
export function usePayrollActions(onDone: (result?: PayrollCalculationResult) => void): { request: (action: PayrollAction, period: PayrollPeriod) => void; dialogs: ReactNode } {
  const { notify } = useToast()
  const navigate = useNavigate()
  const [pending, setPending] = useState<{ action: PayrollAction; period: PayrollPeriod } | null>(null)

  const run = async () => {
    if (!pending) return
    const { action, period } = pending
    switch (action) {
      case 'calculate': {
        const result = await calculatePayroll(period.id)
        notify('success', `Calculated ${result.period.employeeCount} employees` + (result.skipped.length ? `; ${result.skipped.length} skipped.` : '.'))
        setPending(null)
        onDone(result)
        return
      }
      case 'submit':
        await submitPayroll(period.id)
        notify('success', `${period.name} submitted for approval.`)
        break
      case 'approve':
        await approvePayroll(period.id)
        notify('success', `${period.name} approved.`)
        break
      case 'finalize':
        await finalizePayroll(period.id)
        notify('success', `${period.name} finalized and locked.`)
        break
      case 'createBatch':
        return
      case 'generatePayslips': {
        const result = await generatePayslips(period.id)
        notify('success', `${result.generated} payslips generated.`)
        break
      }
      case 'cancel':
        await cancelPayroll(period.id, null)
        notify('success', `${period.name} cancelled.`)
        break
      case 'delete':
        await deletePayrollPeriod(period.id)
        notify('success', `${period.name} deleted.`)
        setPending(null)
        navigate('/payroll/periods')
        return
    }
    setPending(null)
    onDone()
  }

  const batchFor = pending?.action === 'createBatch' ? pending.period : null
  const text = pending ? TEXT[pending.action] : null
  const dialogs = batchFor ? (
    <CreatePaymentBatchModal period={batchFor} onClose={() => setPending(null)} />
  ) : (
    <ConfirmDialog
      open={pending !== null}
      title={text?.title ?? ''}
      message={pending && text ? text.message(pending.period) : ''}
      confirmLabel={text?.confirmLabel}
      tone={text?.tone}
      onCancel={() => setPending(null)}
      onConfirm={run}
    />
  )

  return { request: (action, period) => setPending({ action, period }), dialogs }
}
