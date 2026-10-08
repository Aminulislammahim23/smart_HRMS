import type { ApiResponse } from '../types/api'
import type { AuditLogEntry, AuditLogQuery } from '../types/audit'
import { api, unwrap } from './api'

/** Audit log entries, newest first (Admin only). */
export function getAuditLog(query: AuditLogQuery, signal?: AbortSignal): Promise<AuditLogEntry[]> {
  const params = Object.fromEntries(Object.entries(query).filter(([, value]) => value !== undefined && value !== ''))
  return unwrap(api.get<ApiResponse<AuditLogEntry[]>>('/audit-logs', { params, signal }))
}
