import type { PayrollStatus } from './payroll'

/** Summed payroll amounts of a group of records, as calculated and stored by the server (never recalculated here). */
export interface PayrollAmounts {
  employeeCount: number
  recordCount: number
  basicSalary: number
  houseRent: number
  medicalAllowance: number
  transportAllowance: number
  otherAllowance: number
  totalAllowances: number
  overtime: number
  bonus: number
  grossSalary: number
  tax: number
  providentFund: number
  leaveDeduction: number
  advanceDeduction: number
  loanDeduction: number
  otherDeduction: number
  /** Every deduction except tax. */
  otherDeductions: number
  totalDeduction: number
  netSalary: number
  /** Net salary of paid payslips, and what is still to pay. */
  paidNetSalary: number
  unpaidNetSalary: number
}

/** One row of a grouped report: a payroll period or a department. */
export interface PayrollReportRow extends PayrollAmounts {
  label: string
  payrollPeriodId: string | null
  periodStartDate: string | null
  periodEndDate: string | null
  periodStatus: PayrollStatus | null
}

export interface PayrollReport {
  groupBy: ReportGroupBy
  /** Payroll statuses included (issued payroll by default). */
  statuses: PayrollStatus[]
  totals: PayrollAmounts
  rows: PayrollReportRow[]
}

/** Payroll history at period level. */
export interface PayrollPeriodHistory extends PayrollAmounts {
  id: string
  name: string
  startDate: string
  endDate: string
  month: number
  year: number
  status: PayrollStatus
  isLocked: boolean
  createdAt: string
  /** The "processed" date: last calculation. */
  calculatedAt: string | null
  approvedAt: string | null
  finalizedAt: string | null
  paidAt: string | null
  cancelledAt: string | null
}

export type ReportGroupBy = 'period' | 'department'

export const REPORT_KINDS = ['periods', 'summary', 'departments', 'employees', 'deductions', 'allowances'] as const
export type ReportKind = (typeof REPORT_KINDS)[number]

export type ExportFormat = 'csv' | 'xlsx'

/** Report filters; all optional. Without a status, reports include issued payroll (Approved, Finalized, Paid). */
export interface PayrollReportQuery {
  month?: number
  year?: number
  payrollPeriodId?: string
  employeeId?: string
  departmentId?: string
  designationId?: string
  status?: PayrollStatus
  from?: string
  to?: string
  search?: string
  groupBy?: ReportGroupBy
  page?: number
  pageSize?: number
}

export interface PayrollPeriodHistoryQuery {
  search?: string
  month?: number
  year?: number
  status?: PayrollStatus
  from?: string
  to?: string
  page?: number
  pageSize?: number
}
