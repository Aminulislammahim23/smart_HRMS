import type { ApiResponse } from '../types/api'
import type { EmployeeDocument, UpdateDocumentRequest, UploadDocumentRequest } from '../types/document'
import { api, unwrap, unwrapMessage } from './api'

const base = (employeeId: string) => `/employees/${employeeId}/documents`

/** Newest first. Deactivated documents are included only when includeInactive is true. */
export function getDocuments(employeeId: string, includeInactive: boolean, signal?: AbortSignal): Promise<EmployeeDocument[]> {
  return unwrap(api.get<ApiResponse<EmployeeDocument[]>>(base(employeeId), { params: { includeInactive }, signal }))
}

/** Multipart: file, documentType and an optional description. */
export function uploadDocument(employeeId: string, data: UploadDocumentRequest): Promise<EmployeeDocument> {
  const form = new FormData()
  form.append('file', data.file)
  form.append('documentType', data.documentType)
  if (data.description) form.append('description', data.description)
  return unwrap(api.post<ApiResponse<EmployeeDocument>>(base(employeeId), form))
}

export function updateDocument(employeeId: string, documentId: string, data: UpdateDocumentRequest): Promise<EmployeeDocument> {
  return unwrap(api.put<ApiResponse<EmployeeDocument>>(`${base(employeeId)}/${documentId}`, data))
}

/** Soft delete: the record and file are kept for HR history. */
export function deactivateDocument(employeeId: string, documentId: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`${base(employeeId)}/${documentId}`))
}

/** Streams an active document and saves it under its original file name. */
export async function downloadDocument(document: EmployeeDocument): Promise<void> {
  const response = await api.get<Blob>(`${base(document.employeeId)}/${document.id}/download`, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data)
  const link = window.document.createElement('a')
  link.href = url
  link.download = document.fileName
  link.click()
  // Give the browser time to start the download before the blob is released.
  window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
