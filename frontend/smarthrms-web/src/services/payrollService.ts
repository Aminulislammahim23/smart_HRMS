import type { ApiResponse } from '../types/api'
import type {
  PagedResult,
  PayrollCalculationResult,
  PayrollPeriod,
  PayrollPeriodQuery,
  PayrollPeriodRequest,
  PayrollRecord,
  PayrollHistoryQuery,
  PayrollRecordQuery,
  Payslip,
  PayslipGenerationResult,
  SalaryStructure,
  UpdatePayrollRecordRequest,
  UpdateSalaryStructureRequest,
} from '../types/payroll'
import { api, unwrap, unwrapMessage } from './api'

function params(query: object) {
  return Object.fromEntries(Object.entries(query).filter(([, value]) => value !== undefined && value !== ''))
}

// ---- periods (HR/Admin) ----

export function getPayrollPeriods(query: PayrollPeriodQuery, signal?: AbortSignal): Promise<PayrollPeriod[]> {
  return unwrap(api.get<ApiResponse<PayrollPeriod[]>>('/payroll/periods', { params: params(query), signal }))
}

export function getPayrollPeriod(id: string, signal?: AbortSignal): Promise<PayrollPeriod> {
  return unwrap(api.get<ApiResponse<PayrollPeriod>>(`/payroll/periods/${id}`, { signal }))
}

export function createPayrollPeriod(data: PayrollPeriodRequest): Promise<PayrollPeriod> {
  return unwrap(api.post<ApiResponse<PayrollPeriod>>('/payroll/periods', data))
}

export function updatePayrollPeriod(id: string, data: PayrollPeriodRequest): Promise<PayrollPeriod> {
  return unwrap(api.put<ApiResponse<PayrollPeriod>>(`/payroll/periods/${id}`, data))
}

export function deletePayrollPeriod(id: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`/payroll/periods/${id}`))
}

// ---- calculation and records ----

export function calculatePayroll(periodId: string): Promise<PayrollCalculationResult> {
  return unwrap(api.post<ApiResponse<PayrollCalculationResult>>(`/payroll/calculate/${periodId}`))
}

export function getPayrollRecords(periodId: string, query: PayrollRecordQuery, signal?: AbortSignal): Promise<PayrollRecord[]> {
  return unwrap(api.get<ApiResponse<PayrollRecord[]>>(`/payroll/periods/${periodId}/records`, { params: params(query), signal }))
}

export function getPayrollRecord(id: string, signal?: AbortSignal): Promise<PayrollRecord> {
  return unwrap(api.get<ApiResponse<PayrollRecord>>(`/payroll/records/${id}`, { signal }))
}

export function updatePayrollRecord(id: string, data: UpdatePayrollRecordRequest): Promise<PayrollRecord> {
  return unwrap(api.put<ApiResponse<PayrollRecord>>(`/payroll/records/${id}`, data))
}

// ---- workflow ----

export function submitPayroll(periodId: string): Promise<PayrollPeriod> {
  return unwrap(api.post<ApiResponse<PayrollPeriod>>(`/payroll/${periodId}/submit`))
}

export function approvePayroll(periodId: string): Promise<PayrollPeriod> {
  return unwrap(api.post<ApiResponse<PayrollPeriod>>(`/payroll/${periodId}/approve`))
}

/** Finalizes approved payroll and locks it (Admin). Salaries are then paid through payment batches. */
export function finalizePayroll(periodId: string): Promise<PayrollPeriod> {
  return unwrap(api.post<ApiResponse<PayrollPeriod>>(`/payroll/${periodId}/finalize`))
}

export function cancelPayroll(periodId: string, reason: string | null): Promise<PayrollPeriod> {
  return unwrap(api.post<ApiResponse<PayrollPeriod>>(`/payroll/${periodId}/cancel`, { reason }))
}

// ---- self-service ----

/** Own history (Approved/Paid only), or any employee's for HR/Admin. */
export function getEmployeePayrollHistory(employeeId: string, signal?: AbortSignal): Promise<PayrollRecord[]> {
  return unwrap(api.get<ApiResponse<PayrollRecord[]>>(`/payroll/employee/${employeeId}`, { signal }))
}

export function getPayslip(recordId: string, signal?: AbortSignal): Promise<Payslip> {
  return unwrap(api.get<ApiResponse<Payslip>>(`/payroll/payslip/${recordId}`, { signal }))
}

// ---- payroll history and payslips (paged on the server) ----

/** Every payroll record with its payslip and payment (HR/Admin). */
export function getPayrollHistory(query: PayrollHistoryQuery, signal?: AbortSignal): Promise<PagedResult<PayrollRecord>> {
  return unwrap(api.get<ApiResponse<PagedResult<PayrollRecord>>>('/payroll/history', { params: params(query), signal }))
}

/** Issued payslips only (HR/Admin). */
export function getPayslips(query: PayrollHistoryQuery, signal?: AbortSignal): Promise<PagedResult<PayrollRecord>> {
  return unwrap(api.get<ApiResponse<PagedResult<PayrollRecord>>>('/payroll/payslips', { params: params(query), signal }))
}

/** A payslip by its id (owner or HR/Admin). */
export function getPayslipById(payslipId: string, signal?: AbortSignal): Promise<Payslip> {
  return unwrap(api.get<ApiResponse<Payslip>>(`/payroll/payslips/${payslipId}`, { signal }))
}

/** Issues missing payslips of approved payroll (HR/Admin). */
export function generatePayslips(periodId: string): Promise<PayslipGenerationResult> {
  return unwrap(api.post<ApiResponse<PayslipGenerationResult>>(`/payroll/${periodId}/payslips`))
}

/** The signed-in employee's payslips (employee from the token). */
export function getMyPayslips(query: PayrollHistoryQuery, signal?: AbortSignal): Promise<PagedResult<PayrollRecord>> {
  return unwrap(api.get<ApiResponse<PagedResult<PayrollRecord>>>('/payroll/me/payslips', { params: params(query), signal }))
}

/** The signed-in employee's latest payslip; rejects with 404 while none has been issued. */
export function getMyCurrentPayslip(signal?: AbortSignal): Promise<Payslip> {
  return unwrap(api.get<ApiResponse<Payslip>>('/payroll/me/payslips/current', { signal }))
}

// ---- salary structures ----

export function getSalaryStructures(signal?: AbortSignal): Promise<SalaryStructure[]> {
  return unwrap(api.get<ApiResponse<SalaryStructure[]>>('/payroll/salary-structures', { signal }))
}

export function getSalaryStructure(employeeId: string, signal?: AbortSignal): Promise<SalaryStructure> {
  return unwrap(api.get<ApiResponse<SalaryStructure>>(`/employees/${employeeId}/salary-structure`, { signal }))
}

export function updateSalaryStructure(employeeId: string, data: UpdateSalaryStructureRequest): Promise<SalaryStructure> {
  return unwrap(api.put<ApiResponse<SalaryStructure>>(`/employees/${employeeId}/salary-structure`, data))
}
