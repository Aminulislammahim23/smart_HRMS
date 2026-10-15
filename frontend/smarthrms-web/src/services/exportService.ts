import type { ExportFormat } from '../types/payrollReport'
import { api } from './api'
import { todayInput } from '../utils/formatters'

/**
 * Fetches a file through the API (with the sign-in token) and saves it under `fileName`. The name is chosen here
 * because the browser can't read the server's Content-Disposition across origins. Errors (400/401/403/404) are
 * rejected as ApiError like any other request.
 */
export async function downloadFile(path: string, query: Record<string, unknown>, fileName: string): Promise<void> {
  const response = await api.get<Blob>(path, { params: query, responseType: 'blob' })
  const url = URL.createObjectURL(response.data)
  const link = window.document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  // Give the browser time to start the download before the blob is released.
  window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
}

/** A report or list export saved as "{name}-{yyyy-MM-dd}.{format}". */
export function downloadExport(path: string, query: Record<string, unknown>, name: string, format: ExportFormat): Promise<void> {
  return downloadFile(path, query, `${name}-${todayInput()}.${format}`)
}
