export const PAYMENT_METHODS = ['BankTransfer', 'Cash', 'MobileBanking', 'Cheque', 'Other'] as const
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
}

export interface PaymentStatusHistory {
  /** Null for the creation of the payment. */
  previousStatus: PaymentTransactionStatus | null
  newStatus: PaymentTransactionStatus
  reason: string | null
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
  payrollPeriodId: string
  periodName: string
  payslipId: string | null
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
  actions: PaymentActions
}

export interface CreatePaymentBatchRequest {
  payrollPeriodId: string
  paymentMethod: PaymentMethod
  paymentDate: string | null
  notes: string | null
}

export interface MarkPaymentPaidRequest {
  transactionReference: string | null
  paymentDate: string | null
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
