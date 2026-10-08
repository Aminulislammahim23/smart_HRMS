import type { ApiResponse } from '../types/api'
import type { PagedResult, PayrollRecord } from '../types/payroll'
import type {
  ExportFormat,
  PayrollPeriodHistory,
  PayrollPeriodHistoryQuery,
  PayrollReport,
  PayrollReportQuery,
  ReportKind,
} from '../types/payrollReport'
import { api, unwrap } from './api'
import { downloadExport } from './exportService'

function params(query: object) {
  return Object.fromEntries(Object.entries(query).filter(([, value]) => value !== undefined && value !== ''))
}

/** Payroll history at period level, with totals (HR/Admin), paged on the server. */
export function getPayrollPeriodHistory(query: PayrollPeriodHistoryQuery, signal?: AbortSignal): Promise<PagedResult<PayrollPeriodHistory>> {
  return unwrap(api.get<ApiResponse<PagedResult<PayrollPeriodHistory>>>('/payroll/reports/periods', { params: params(query), signal }))
}

/** summary (by period), departments, deductions or allowances. */
export function getPayrollReport(kind: Exclude<ReportKind, 'periods' | 'employees'>, query: PayrollReportQuery, signal?: AbortSignal): Promise<PayrollReport> {
  return unwrap(api.get<ApiResponse<PayrollReport>>(`/payroll/reports/${kind}`, { params: params(query), signal }))
}

/** One row per employee and payroll period, paged on the server. */
export function getEmployeePayrollReport(query: PayrollReportQuery, signal?: AbortSignal): Promise<PagedResult<PayrollRecord>> {
  return unwrap(api.get<ApiResponse<PagedResult<PayrollRecord>>>('/payroll/reports/employees', { params: params(query), signal }))
}

/** Downloads a report with the same filters (paging is ignored: the whole result is exported). */
export function exportPayrollReport(kind: ReportKind, query: PayrollReportQuery, format: ExportFormat): Promise<void> {
  const filters = { ...query, page: undefined, pageSize: undefined }
  return downloadExport(`/payroll/reports/${kind}/export`, { ...params(filters), format }, `payroll-${kind}`, format)
}
