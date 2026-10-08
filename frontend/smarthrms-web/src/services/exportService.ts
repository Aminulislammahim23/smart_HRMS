import type { ExportFormat } from '../types/payrollReport'
import { api } from './api'
import { todayInput } from '../utils/formatters'

/**
 * Fetches an export through the API (with the sign-in token) and saves it. The file name is built here
 * ("{name}-{yyyy-MM-dd}.{format}") because the browser can't read the server's Content-Disposition across origins.
 * Errors (400/401/403) are rejected as ApiError like any other request.
 */
export async function downloadExport(path: string, query: Record<string, unknown>, name: string, format: ExportFormat): Promise<void> {
  const response = await api.get<Blob>(path, { params: query, responseType: 'blob' })
  const url = URL.createObjectURL(response.data)
  const link = window.document.createElement('a')
  link.href = url
  link.download = `${name}-${todayInput()}.${format}`
  link.click()
  // Give the browser time to start the download before the blob is released.
  window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
