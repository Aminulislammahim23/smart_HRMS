/** AuditLogDto: one recorded action. Details never contain salary amounts. */
export interface AuditLogEntry {
  id: string
  occurredAt: string
  userId: string | null
  username: string | null
  action: string
  entityType: string
  entityId: string | null
  details: string | null
}

export interface AuditLogQuery {
  /** "payroll": payroll periods, records, payslips, salary structures, payments and report exports. */
  category?: 'payroll'
  entityType?: string
  entityId?: string
  action?: string
  username?: string
  /** Office dates, inclusive. */
  from?: string
  to?: string
  /** 1–500, default 100. */
  take?: number
}

/** The audit actions written by payroll, payslip, payment and report features. */
export const PAYROLL_AUDIT_ACTIONS = [
  'PayrollPeriodCreated',
  'PayrollPeriodUpdated',
  'PayrollPeriodDeleted',
  'PayrollCalculated',
  'PayrollRecordUpdated',
  'PayrollSubmitted',
  'PayrollApproved',
  'PayrollFinalized',
  'PayrollLocked',
  'PayrollPaid',
  'PayrollCancelled',
  'PayslipsGenerated',
  'PayslipViewed',
  'PayslipsExported',
  'PayrollReportExported',
  'SalaryStructureUpdated',
  'PaymentBatchCreated',
  'PaymentBatchCancelled',
  'PaymentProcessingStarted',
  'PaymentMarkedPaid',
  'PaymentMarkedFailed',
  'PaymentRetried',
  'PaymentCancelled',
] as const
