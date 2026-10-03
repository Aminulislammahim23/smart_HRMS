import { BadgeCheck, Banknote, Clock, MinusCircle, Users, Wallet, type LucideIcon } from 'lucide-react'
import { formatAmount } from '../../utils/formatters'

interface Card {
  label: string
  value: string
  hint?: string
  icon: LucideIcon
  tone: string
}

interface PayrollSummaryCardsProps {
  employees: number
  gross: number
  deduction: number
  net: number
  /** Dashboard only: periods waiting for approval and periods already paid. */
  pendingApproval?: number
  paid?: number
  hint?: string
}

/** Headline payroll numbers. Amounts are totals of the server-calculated records (no client-side maths). */
export function PayrollSummaryCards({ employees, gross, deduction, net, pendingApproval, paid, hint }: PayrollSummaryCardsProps) {
  const cards: Card[] = [
    { label: 'Employees', value: String(employees), hint, icon: Users, tone: 'bg-primary/10 text-primary' },
    { label: 'Gross salary', value: formatAmount(gross), hint, icon: Banknote, tone: 'bg-info/10 text-info' },
    { label: 'Total deduction', value: formatAmount(deduction), hint, icon: MinusCircle, tone: 'bg-error/10 text-error' },
    { label: 'Net salary', value: formatAmount(net), hint, icon: Wallet, tone: 'bg-success/10 text-success' },
  ]
  if (pendingApproval !== undefined) {
    cards.push({ label: 'Pending approval', value: String(pendingApproval), hint: 'Payroll periods', icon: Clock, tone: 'bg-warning/10 text-warning' })
  }
  if (paid !== undefined) {
    cards.push({ label: 'Paid payroll', value: String(paid), hint: 'Payroll periods', icon: BadgeCheck, tone: 'bg-success/10 text-success' })
  }

  return (
    <div className={`grid gap-4 sm:grid-cols-2 ${cards.length > 4 ? 'xl:grid-cols-3 2xl:grid-cols-6' : 'xl:grid-cols-4'}`}>
      {cards.map(({ label, value, hint: cardHint, icon: Icon, tone }) => (
        <div key={label} className="card bg-base-100 shadow-sm">
          <div className="card-body flex-row items-center gap-4 p-5">
            <div className={`rounded-xl p-3 ${tone}`}>
              <Icon className="size-6" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-base-content/60">{label}</p>
              <p className="truncate text-xl font-semibold tabular-nums">{value}</p>
              {cardHint && <p className="truncate text-xs text-base-content/50">{cardHint}</p>}
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}
