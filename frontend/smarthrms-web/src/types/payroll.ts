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
  /** Approved/Paid payroll with records that still lack a payslip. */
  canGeneratePayslips: boolean
}

export const PAYMENT_STATUSES = ['Unpaid', 'Paid'] as const
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number]

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
  /** Payslips issued (every record once approved) and how many are paid. */
  payslipCount: number
  paidPayslipCount: number
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
  providentFund: number
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
  /** The issued payslip (once approved); null before. */
  payslipId: string | null
  payslipNumber: string | null
  payslipGeneratedAt: string | null
  /** Null while no payslip has been issued. */
  paymentStatus: PaymentStatus | null
  paymentDate: string | null
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
  /** Null for an HR/Admin preview of payroll that is not approved yet. */
  payslipId: string | null
  payslipNumber: string | null
  recordId: string
  payrollPeriodId: string
  companyName: string
  companyAddress: string | null
  currency: string
  employeeId: string
  employeeCode: string
  employeeName: string
  /** Relative URL of the employee's current photo, or null. */
  employeePhotoUrl: string | null
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
  payrollStatus: PayrollStatus
  paymentStatus: PaymentStatus
  /** Date the salary was paid ("yyyy-MM-dd"). */
  paymentDate: string | null
  /** When the payslip was issued (at approval); null for a preview. */
  generatedAt: string | null
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
  /** Monthly provident fund contribution (employee share), withheld from salary. */
  monthlyProvidentFund: number
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
  monthlyProvidentFund: number
}

/** PagedResult<T>: one page of a server-side paged list (pages are 1-based). */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export const HISTORY_SORTS = ['period', 'employee', 'gross', 'net', 'paymentDate'] as const
export type HistorySort = (typeof HISTORY_SORTS)[number]

/** Filters and paging for payroll history and payslip lists (all optional). */
export interface PayrollHistoryQuery {
  employeeId?: string
  departmentId?: string
  month?: number
  year?: number
  status?: PayrollStatus
  paymentStatus?: PaymentStatus
  search?: string
  page?: number
  pageSize?: number
  sortBy?: HistorySort
  sortDirection?: 'asc' | 'desc'
}

export interface PayslipGenerationResult {
  payrollPeriodId: string
  generated: number
  alreadyGenerated: number
}

export function isPaymentStatus(value: string): value is PaymentStatus {
  return PAYMENT_STATUSES.some((status) => status === value)
}

export function isPayrollStatus(value: string): value is PayrollStatus {
  return PAYROLL_STATUSES.some((status) => status === value)
}

export function isPayrollRecordStatus(value: string): value is PayrollRecordStatus {
  return PAYROLL_RECORD_STATUSES.some((status) => status === value)
}
