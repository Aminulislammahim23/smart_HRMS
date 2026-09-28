import type { ApiResponse } from '../types/api'
import type {
  EmployeeDocument,
  UpdateDocumentRequest,
  UploadDocumentRequest,
  UploadProgressHandler,
} from '../types/document'
import { api, unwrap, unwrapMessage } from './api'

// Documents are always addressed through their employee (the backend's route convention), so a document id
// only works together with the employee it belongs to.
const base = (employeeId: string) => `/employees/${employeeId}/documents`

/** Newest first. Deactivated documents are included only when includeInactive is true. */
export function getDocuments(employeeId: string, includeInactive: boolean, signal?: AbortSignal): Promise<EmployeeDocument[]> {
  return unwrap(api.get<ApiResponse<EmployeeDocument[]>>(base(employeeId), { params: { includeInactive }, signal }))
}

export function getDocument(employeeId: string, documentId: string, signal?: AbortSignal): Promise<EmployeeDocument> {
  return unwrap(api.get<ApiResponse<EmployeeDocument>>(`${base(employeeId)}/${documentId}`, { signal }))
}

/** Multipart: file, documentType, documentName, and optional dates and description. */
export function uploadDocument(employeeId: string, data: UploadDocumentRequest, onProgress?: UploadProgressHandler): Promise<EmployeeDocument> {
  const form = new FormData()
  form.append('file', data.file)
  form.append('documentType', data.documentType)
  form.append('documentName', data.documentName)
  if (data.issueDate) form.append('issueDate', data.issueDate)
  if (data.expiryDate) form.append('expiryDate', data.expiryDate)
  if (data.description) form.append('description', data.description)

  return unwrap(
    api.post<ApiResponse<EmployeeDocument>>(base(employeeId), form, {
      onUploadProgress: (event) => {
        if (onProgress && event.total) onProgress(Math.round((event.loaded / event.total) * 100))
      },
    }),
  )
}

/** Replaces the metadata; the stored file is not touched. */
export function updateDocument(employeeId: string, documentId: string, data: UpdateDocumentRequest): Promise<EmployeeDocument> {
  return unwrap(api.put<ApiResponse<EmployeeDocument>>(`${base(employeeId)}/${documentId}`, data))
}

/** Soft delete: the record and file are kept for HR history but hidden and no longer downloadable. */
export function deactivateDocument(employeeId: string, documentId: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`${base(employeeId)}/${documentId}`))
}

/** Fetches the file through the API download endpoint (never a direct storage URL). */
export async function fetchDocumentFile(document: EmployeeDocument): Promise<Blob> {
  const response = await api.get<Blob>(`${base(document.employeeId)}/${document.id}/download`, { responseType: 'blob' })
  return response.data
}

/** Streams an active document and saves it under its original file name. */
export async function downloadDocument(document: EmployeeDocument): Promise<void> {
  const url = URL.createObjectURL(await fetchDocumentFile(document))
  const link = window.document.createElement('a')
  link.href = url
  link.download = document.fileName
  link.click()
  // Give the browser time to start the download before the blob is released.
  window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
}

/** PDFs and images can be shown in the browser; Word files are download-only. */
export function canPreview(document: EmployeeDocument): boolean {
  return document.contentType === 'application/pdf' || document.contentType.startsWith('image/')
}
