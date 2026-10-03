import { useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { useToast } from '../../hooks/useToast'
import { approvePayroll, calculatePayroll, cancelPayroll, deletePayrollPeriod, markPayrollPaid, submitPayroll } from '../../services/payrollService'
import type { PayrollCalculationResult, PayrollPeriod } from '../../types/payroll'
import { formatAmount } from '../../utils/formatters'
import { ConfirmDialog } from '../common/ConfirmDialog'

export type PayrollAction = 'calculate' | 'submit' | 'approve' | 'markPaid' | 'cancel' | 'delete'

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
    message: (p) => `Approve ${p.name}: ${p.employeeCount} employees, net total ${formatAmount(p.netTotal)}. Approved payroll can't be edited, recalculated or cancelled, and employees can see their payslips.`,
    confirmLabel: 'Approve',
    tone: 'primary',
  },
  markPaid: {
    title: 'Mark as paid?',
    message: (p) => `Confirm that ${p.name} (net ${formatAmount(p.netTotal)}) has been paid. This is final.`,
    confirmLabel: 'Mark as paid',
    tone: 'primary',
  },
  cancel: {
    title: 'Cancel payroll?',
    message: (p) => `Cancel ${p.name}? Its records are kept for history but it can't be processed any more. A new period with the same dates can be created afterwards.`,
    confirmLabel: 'Cancel payroll',
    tone: 'danger',
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
      case 'markPaid':
        await markPayrollPaid(period.id)
        notify('success', `${period.name} marked as paid.`)
        break
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

  const text = pending ? TEXT[pending.action] : null
  const dialogs = (
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
