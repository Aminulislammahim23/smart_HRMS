import type { ApiResponse } from '../types/api'
import type {
  CreatePaymentBatchRequest,
  MarkPaymentPaidRequest,
  Payment,
  PaymentBatch,
  PaymentBatchQuery,
  PaymentQuery,
} from '../types/payment'
import type { PagedResult } from '../types/payroll'
import { api, unwrap } from './api'

function params(query: object) {
  return Object.fromEntries(Object.entries(query).filter(([, value]) => value !== undefined && value !== ''))
}

// ---- batches (view: HR/Admin; changes: Admin) ----

/** Creates a batch paying every unpaid payslip of a finalized payroll; the server calculates the totals. */
export function createPaymentBatch(data: CreatePaymentBatchRequest): Promise<PaymentBatch> {
  return unwrap(api.post<ApiResponse<PaymentBatch>>('/payments/batches', data))
}

export function getPaymentBatches(query: PaymentBatchQuery, signal?: AbortSignal): Promise<PagedResult<PaymentBatch>> {
  return unwrap(api.get<ApiResponse<PagedResult<PaymentBatch>>>('/payments/batches', { params: params(query), signal }))
}

export function getPaymentBatch(id: string, signal?: AbortSignal): Promise<PaymentBatch> {
  return unwrap(api.get<ApiResponse<PaymentBatch>>(`/payments/batches/${id}`, { signal }))
}

/** Moves every Pending payment of the batch to Processing. */
export function processPaymentBatch(id: string): Promise<PaymentBatch> {
  return unwrap(api.post<ApiResponse<PaymentBatch>>(`/payments/batches/${id}/process`))
}

/** Cancels the batch while all its payments are still Pending; the payroll can then be paid in a new batch. */
export function cancelPaymentBatch(id: string, reason: string): Promise<PaymentBatch> {
  return unwrap(api.post<ApiResponse<PaymentBatch>>(`/payments/batches/${id}/cancel`, { reason }))
}

// ---- payments ----

/** Payment history across employees (HR/Admin), paged on the server. */
export function getPayments(query: PaymentQuery, signal?: AbortSignal): Promise<PagedResult<Payment>> {
  return unwrap(api.get<ApiResponse<PagedResult<Payment>>>('/payments', { params: params(query), signal }))
}

/** The signed-in employee's own salary payments (employee from the token). */
export function getMyPayments(query: PaymentQuery, signal?: AbortSignal): Promise<PagedResult<Payment>> {
  return unwrap(api.get<ApiResponse<PagedResult<Payment>>>('/payments/me', { params: params(query), signal }))
}

/** One payment with its status history (owner or HR/Admin). */
export function getPayment(id: string, signal?: AbortSignal): Promise<Payment> {
  return unwrap(api.get<ApiResponse<Payment>>(`/payments/${id}`, { signal }))
}

export function markPaymentPaid(id: string, data: MarkPaymentPaidRequest): Promise<Payment> {
  return unwrap(api.post<ApiResponse<Payment>>(`/payments/${id}/paid`, data))
}

export function markPaymentFailed(id: string, reason: string): Promise<Payment> {
  return unwrap(api.post<ApiResponse<Payment>>(`/payments/${id}/failed`, { reason }))
}

export function retryPayment(id: string, reason: string | null): Promise<Payment> {
  return unwrap(api.post<ApiResponse<Payment>>(`/payments/${id}/retry`, { reason }))
}

export function cancelPayment(id: string, reason: string): Promise<Payment> {
  return unwrap(api.post<ApiResponse<Payment>>(`/payments/${id}/cancel`, { reason }))
}
