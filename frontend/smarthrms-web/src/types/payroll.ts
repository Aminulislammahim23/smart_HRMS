export const PAYROLL_STATUSES = ['Draft', 'Calculated', 'PendingApproval', 'Approved', 'Paid', 'Cancelled'] as const
export type PayrollStatus = (typeof PAYROLL_STATUSES)[number]

export const PAYROLL_RECORD_STATUSES = ['Calculated', 'NeedsReview', 'Approved', 'Paid', 'Cancelled'] as const
export type PayrollRecordStatus = (typeof PAYROLL_RECORD_STATUSES)[number]

/** What the signed-in user may do next, computed by the server (which checks again on every action). */
export interface PayrollActions {
  canEdit: boolean
  canDelete: boolean
  canCalculate: boolean
  canEditRecords: boolean
  canSubmit: boolean
  canApprove: boolean
  canMarkPaid: boolean
  canCancel: boolean
}

/** PayrollPeriodDto. Dates are "yyyy-MM-dd"; *At values are UTC instants. Amounts are plain decimals. */
export interface PayrollPeriod {
  id: string
  name: string
  startDate: string
  endDate: string
  status: PayrollStatus
  notes: string | null
  workingDays: number
  employeeCount: number
  grossTotal: number
  deductionTotal: number
  netTotal: number
  needsReviewCount: number
  createdBy: string | null
  createdAt: string
  updatedAt: string | null
  calculatedAt: string | null
  calculatedBy: string | null
  submittedAt: string | null
  submittedBy: string | null
  approvedAt: string | null
  approvedBy: string | null
  paidAt: string | null
  paidBy: string | null
  cancelledAt: string | null
  cancelledBy: string | null
  actions: PayrollActions
}

/** PayrollRecordDto: one employee's salary for one period. */
export interface PayrollRecord {
  id: string
  payrollPeriodId: string
  periodName: string
  periodStartDate: string
  periodEndDate: string
  periodStatus: PayrollStatus
  employeeId: string
  employeeCode: string
  employeeName: string
  departmentName: string | null
  designationName: string | null
  basicSalary: number
  houseRent: number
  medicalAllowance: number
  transportAllowance: number
  otherAllowance: number
  overtimeAmount: number
  bonus: number
  grossSalary: number
  tax: number
  leaveDeduction: number
  advanceDeduction: number
  loanDeduction: number
  otherDeduction: number
  totalDeduction: number
  netSalary: number
  workingDays: number
  presentDays: number
  paidLeaveDays: number
  unpaidLeaveDays: number
  absentDays: number
  status: PayrollRecordStatus
  remarks: string | null
  canEdit: boolean
  createdAt: string
  updatedAt: string | null
}

export interface PayrollPeriodRequest {
  name: string | null
  startDate: string
  endDate: string
  notes: string | null
}

/** The manually entered parts of a record; everything else comes from salary structure and attendance. */
export interface UpdatePayrollRecordRequest {
  overtimeAmount: number
  bonus: number
  advanceDeduction: number
  loanDeduction: number
  otherDeduction: number
  remarks: string | null
}

export interface PayrollPeriodQuery {
  year?: number
  status?: PayrollStatus
}

export interface PayrollRecordQuery {
  search?: string
  departmentId?: string
  designationId?: string
  status?: PayrollRecordStatus
}

export interface SkippedEmployee {
  employeeId: string
  employeeCode: string
  employeeName: string
  reason: string
}

export interface PayrollCalculationResult {
  period: PayrollPeriod
  created: number
  updated: number
  removed: number
  skipped: SkippedEmployee[]
}

export interface PayslipLine {
  label: string
  amount: number
}

export interface Payslip {
  recordId: string
  companyName: string
  companyAddress: string | null
  currency: string
  employeeId: string
  employeeCode: string
  employeeName: string
  departmentName: string | null
  designationName: string | null
  periodName: string
  periodStartDate: string
  periodEndDate: string
  earnings: PayslipLine[]
  deductions: PayslipLine[]
  grossSalary: number
  totalDeduction: number
  netSalary: number
  workingDays: number
  presentDays: number
  paidLeaveDays: number
  unpaidLeaveDays: number
  absentDays: number
  paymentStatus: PayrollStatus
  /** False for a preview of payroll that isn't approved yet (HR/Admin only). */
  isFinal: boolean
  approvedBy: string | null
  approvedAt: string | null
  paidAt: string | null
  remarks: string | null
}

/** SalaryStructureDto: basic salary (employee record) plus allowances and monthly tax. */
export interface SalaryStructure {
  employeeId: string
  employeeCode: string
  employeeName: string
  departmentName: string | null
  designationName: string | null
  employeeStatus: string
  basicSalary: number | null
  houseRent: number
  medicalAllowance: number
  transportAllowance: number
  otherAllowance: number
  monthlyTax: number
  monthlyGross: number | null
  updatedAt: string | null
}

export interface UpdateSalaryStructureRequest {
  basicSalary: number
  houseRent: number
  medicalAllowance: number
  transportAllowance: number
  otherAllowance: number
  monthlyTax: number
}

export function isPayrollStatus(value: string): value is PayrollStatus {
  return PAYROLL_STATUSES.some((status) => status === value)
}

export function isPayrollRecordStatus(value: string): value is PayrollRecordStatus {
  return PAYROLL_RECORD_STATUSES.some((status) => status === value)
}
