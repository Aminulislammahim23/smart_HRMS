import type { PagedResult } from './payroll'

export const PAYMENT_METHODS =['BankTransfer', 'Cash', 'MobileBanking', 'Cheque', 'Other'] as const
export type PaymentMethod = (typeof PAYMENT_METHODS)[number]

/** One salary payment: Pending → Processing → Paid, or Failed (retry → Processing); Pending can be cancelled. */
export const PAYMENT_TRANSACTION_STATUSES = ['Pending', 'Processing', 'Paid', 'Failed', 'Cancelled'] as const
export type PaymentTransactionStatus = (typeof PAYMENT_TRANSACTION_STATUSES)[number]

/** Derived by the server from the batch's payments. */
export const PAYMENT_BATCH_STATUSES = ['Pending', 'Processing', 'PartiallyPaid', 'Paid', 'Failed', 'Cancelled'] as const
export type PaymentBatchStatus = (typeof PAYMENT_BATCH_STATUSES)[number]

export interface PaymentCounts {
  pending: number
  processing: number
  paid: number
  failed: number
  cancelled: number
}

/** What the signed-in user may do with a batch, computed by the server (which checks again). */
export interface PaymentBatchActions {
  canProcess: boolean
  canCancel: boolean
}

/** PaymentBatchDto. Totals are calculated by the server; cancelled payments are excluded. */
export interface PaymentBatch {
  id: string
  batchNumber: string
  payrollPeriodId: string
  periodName: string
  periodStartDate: string
  periodEndDate: string
  /** Planned payment date ("yyyy-MM-dd"). */
  paymentDate: string
  paymentMethod: PaymentMethod
  totalEmployees: number
  totalAmount: number
  paidAmount: number
  status: PaymentBatchStatus
  notes: string | null
  counts: PaymentCounts
  createdBy: string | null
  createdAt: string
  updatedAt: string | null
  actions: PaymentBatchActions
}

export interface PaymentActions {
  canMarkPaid: boolean
  canMarkFailed: boolean
  canRetry: boolean
  canCancel: boolean
  /** Day 21: Admin, Paid: correct the reference or date (never the amount or status). */
  canCorrect: boolean
}

export interface PaymentStatusHistory {
  /** Null for the creation of the payment. Paid → Paid is a correction. */
  previousStatus: PaymentTransactionStatus | null
  newStatus: PaymentTransactionStatus
  reason: string | null
  /** Transfer reference of this step: confirmed, failed attempt or correction. */
  reference: string | null
  changedBy: string | null
  changedAt: string
}

/** PaymentDto: one employee's salary payment. */
export interface Payment {
  id: string
  paymentBatchId: string
  batchNumber: string
  paymentBatchItemId: string
  employeeId: string
  employeeCode: string
  employeeName: string
  /** Department stored on the payroll record. */
  departmentName: string | null
  payrollPeriodId: string
  periodName: string
  payslipId: string | null
  payslipNumber: string | null
  /** The finalized net salary of the locked payroll record. */
  amount: number
  paymentMethod: PaymentMethod
  status: PaymentTransactionStatus
  transactionReference: string | null
  failureReason: string | null
  /** The date paid once Paid; otherwise the batch's planned date. */
  paymentDate: string | null
  processedAt: string | null
  createdAt: string
  updatedAt: string | null
  /** Status changes, oldest first (single-payment endpoint only). */
  history: PaymentStatusHistory[] | null
  /** Processing attempts and failed attempts (single-payment endpoint only). */
  attemptCount: number | null
  failedAttemptCount: number | null
  actions: PaymentActions
}

export interface CreatePaymentBatchRequest {
  payrollPeriodId: string
  paymentMethod: PaymentMethod
  paymentDate: string | null
  notes: string | null
}

export interface MarkPaymentPaidRequest {
  /** Required for BankTransfer, MobileBanking and Cheque; Cash/Other need a reference or remarks. */
  transactionReference: string | null
  paymentDate: string | null
  remarks: string | null
}

export interface MarkPaymentFailedRequest {
  reason: string
  /** The failed attempt's provider reference, kept in the history. */
  transactionReference: string | null
}

export interface RetryPaymentRequest {
  reason: string | null
  /** Another method for the new attempt; null keeps the current one. */
  paymentMethod: PaymentMethod | null
}

/** Correction of a Paid payment: blank fields stay unchanged; the reason is required. */
export interface CorrectPaymentRequest {
  transactionReference: string | null
  paymentDate: string | null
  reason: string
}

/** Methods whose confirmation needs a transaction reference (mirrors the server rule). */
export function requiresReference(method: PaymentMethod): boolean {
  return method === 'BankTransfer' || method === 'MobileBanking' || method === 'Cheque'
}

/**
 * Disbursement figures of finalized or paid payroll, computed by the server. Payable = finalized net salaries above
 * zero with a payslip; outstanding = payable − paid; not initiated = unpaid and in no active payment.
 */
export interface DisbursementFigures {
  payableCount: number
  payableAmount: number
  paidCount: number
  paidAmount: number
  outstandingAmount: number
  notInitiatedCount: number
  notInitiatedAmount: number
  pendingCount: number
  pendingAmount: number
  processingCount: number
  processingAmount: number
  failedCount: number
  failedAmount: number
}

export interface PayrollDisbursement extends DisbursementFigures {
  payrollPeriodId: string
  periodName: string
  startDate: string
  endDate: string
  payrollStatus: 'Finalized' | 'Paid'
  hasOpenBatch: boolean
  canCreateBatch: boolean
}

export interface DisbursementSummary {
  totals: DisbursementFigures
  payrolls: PagedResult<PayrollDisbursement>
}

export interface DisbursementSummaryQuery {
  payrollPeriodId?: string
  year?: number
  page?: number
  pageSize?: number
}

export interface PaymentBatchQuery {
  payrollPeriodId?: string
  status?: PaymentBatchStatus
  page?: number
  pageSize?: number
}

export interface PaymentQuery {
  batchId?: string
  employeeId?: string
  departmentId?: string
  payrollPeriodId?: string
  status?: PaymentTransactionStatus
  paymentMethod?: PaymentMethod
  from?: string
  to?: string
  search?: string
  page?: number
  pageSize?: number
}

export function isPaymentMethod(value: string): value is PaymentMethod {
  return PAYMENT_METHODS.some((method) => method === value)
}

export function isPaymentTransactionStatus(value: string): value is PaymentTransactionStatus {
  return PAYMENT_TRANSACTION_STATUSES.some((status) => status === value)
}

export function isPaymentBatchStatus(value: string): value is PaymentBatchStatus {
  return PAYMENT_BATCH_STATUSES.some((status) => status === value)
}
