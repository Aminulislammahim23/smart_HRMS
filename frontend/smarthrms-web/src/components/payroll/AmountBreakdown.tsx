import type { PayrollRecord } from '../../types/payroll'
import { formatAmount } from '../../utils/formatters'
import { deductionLines, earningLines, type AmountLine } from './payrollLines'

/** A titled list of amounts with its total (earnings or deductions). Zero lines are shown dimmed, never hidden. */
export function AmountBreakdown({ title, lines, totalLabel, total, tone = 'default' }: { title: string; lines: AmountLine[]; totalLabel: string; total: number; tone?: 'default' | 'deduction' }) {
  return (
    <div className="rounded-box border border-base-300">
      <h3 className="border-b border-base-300 px-4 py-2 text-sm font-semibold">{title}</h3>
      <dl className="divide-y divide-base-200 text-sm">
        {lines.map(({ label, amount }) => (
          <div key={label} className={`flex justify-between gap-4 px-4 py-2 ${amount === 0 ? 'text-base-content/45' : ''}`}>
            <dt>{label}</dt>
            <dd className="tabular-nums">{formatAmount(amount)}</dd>
          </div>
        ))}
        <div className={`flex justify-between gap-4 bg-base-200/60 px-4 py-2 font-semibold ${tone === 'deduction' ? 'text-error' : ''}`}>
          <dt>{totalLabel}</dt>
          <dd className="tabular-nums">{formatAmount(total)}</dd>
        </div>
      </dl>
    </div>
  )
}

/** Salary earnings of a payroll record. */
export function SalaryBreakdown({ record }: { record: PayrollRecord }) {
  return <AmountBreakdown title="Earnings" lines={earningLines(record)} totalLabel="Gross salary" total={record.grossSalary} />
}

/** Deductions of a payroll record. */
export function DeductionBreakdown({ record }: { record: PayrollRecord }) {
  return <AmountBreakdown title="Deductions" lines={deductionLines(record)} totalLabel="Total deduction" total={record.totalDeduction} tone="deduction" />
}

/** Working, present, leave and absent days the calculation used. */
export function PayrollDays({ days }: { days: Pick<PayrollRecord, 'workingDays' | 'presentDays' | 'paidLeaveDays' | 'unpaidLeaveDays' | 'absentDays'> }) {
  const items = [
    { label: 'Working days', value: days.workingDays },
    { label: 'Present', value: days.presentDays },
    { label: 'Paid leave', value: days.paidLeaveDays },
    { label: 'Unpaid leave', value: days.unpaidLeaveDays },
    { label: 'Absent', value: days.absentDays },
  ]
  return (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
      {items.map(({ label, value }) => (
        <div key={label} className="rounded-box bg-base-200/60 px-3 py-2 text-center">
          <p className="text-lg font-semibold tabular-nums">{value}</p>
          <p className="text-xs text-base-content/60">{label}</p>
        </div>
      ))}
    </div>
  )
}
