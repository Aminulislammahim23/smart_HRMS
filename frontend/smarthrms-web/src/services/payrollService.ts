import type { ApiResponse } from '../types/api'
import type {
  PayrollCalculationResult,
  PayrollPeriod,
  PayrollPeriodQuery,
  PayrollPeriodRequest,
  PayrollRecord,
  PayrollRecordQuery,
  Payslip,
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

export function markPayrollPaid(periodId: string): Promise<PayrollPeriod> {
  return unwrap(api.post<ApiResponse<PayrollPeriod>>(`/payroll/${periodId}/mark-paid`))
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
