import type { PayrollRecord } from '../../types/payroll'

export interface AmountLine {
  label: string
  amount: number
}

export function earningLines(record: PayrollRecord): AmountLine[] {
  return [
    { label: 'Basic salary', amount: record.basicSalary },
    { label: 'House rent', amount: record.houseRent },
    { label: 'Medical allowance', amount: record.medicalAllowance },
    { label: 'Transport allowance', amount: record.transportAllowance },
    { label: 'Other allowance', amount: record.otherAllowance },
    { label: 'Overtime', amount: record.overtimeAmount },
    { label: 'Bonus', amount: record.bonus },
  ]
}

export function deductionLines(record: PayrollRecord): AmountLine[] {
  return [
    { label: 'Tax', amount: record.tax },
    { label: 'Provident fund', amount: record.providentFund },
    { label: 'Unpaid leave', amount: record.leaveDeduction },
    { label: 'Advance', amount: record.advanceDeduction },
    { label: 'Loan', amount: record.loanDeduction },
    { label: 'Other deduction', amount: record.otherDeduction },
  ]
}
