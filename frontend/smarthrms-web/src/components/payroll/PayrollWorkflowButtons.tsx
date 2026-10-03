import { BadgeCheck, Ban, Calculator, FileStack, Send, Trash2, Wallet } from 'lucide-react'
import type { PayrollPeriod } from '../../types/payroll'
import type { PayrollAction } from './usePayrollActions'

const BUTTONS: { action: PayrollAction; label: string; icon: typeof Calculator; className: string; allowed: (p: PayrollPeriod) => boolean }[] = [
  { action: 'calculate', label: 'Calculate', icon: Calculator, className: 'btn-primary', allowed: (p) => p.actions.canCalculate },
  { action: 'submit', label: 'Submit for approval', icon: Send, className: 'btn-primary', allowed: (p) => p.actions.canSubmit },
  { action: 'approve', label: 'Approve', icon: BadgeCheck, className: 'btn-success', allowed: (p) => p.actions.canApprove },
  { action: 'markPaid', label: 'Mark as paid', icon: Wallet, className: 'btn-success', allowed: (p) => p.actions.canMarkPaid },
  { action: 'generatePayslips', label: 'Generate payslips', icon: FileStack, className: 'btn-primary', allowed: (p) => p.actions.canGeneratePayslips },
  { action: 'cancel', label: 'Cancel payroll', icon: Ban, className: 'btn-ghost text-error', allowed: (p) => p.actions.canCancel },
  { action: 'delete', label: 'Delete', icon: Trash2, className: 'btn-ghost text-error', allowed: (p) => p.actions.canDelete },
]

/**
 * The next workflow steps the signed-in user may take, as decided by the server (period.actions). A step the user
 * can't take is not shown at all.
 */
export function PayrollWorkflowButtons({ period, onAction, size = 'sm' }: { period: PayrollPeriod; onAction: (action: PayrollAction) => void; size?: 'sm' | 'xs' }) {
  const buttons = BUTTONS.filter((button) => button.allowed(period))
  if (buttons.length === 0) return null

  return (
    <div className="flex flex-wrap gap-2">
      {buttons.map(({ action, label, icon: Icon, className }) => (
        <button key={action} type="button" className={`btn btn-${size} ${className}`} onClick={() => onAction(action)}>
          <Icon className="size-4" /> {label}
        </button>
      ))}
    </div>
  )
}
