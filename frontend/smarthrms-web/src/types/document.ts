export const DOCUMENT_TYPES = [
  'Nid',
  'Passport',
  'EducationalCertificate',
  'ExperienceCertificate',
  'Cv',
  'JoiningLetter',
  'ContractPaper',
  'Other',
] as const
export type DocumentType = (typeof DOCUMENT_TYPES)[number]

/** EmployeeDocumentDto */
export interface EmployeeDocument {
  id: string
  employeeId: string
  documentType: DocumentType
  fileName: string
  contentType: string
  fileSizeBytes: number
  description: string | null
  isActive: boolean
  uploadedAt: string
  updatedAt: string | null
  /** API route that streams the file. */
  downloadUrl: string
}

/** Multipart form for POST /api/employees/{id}/documents */
export interface UploadDocumentRequest {
  file: File
  documentType: DocumentType
  description: string | null
}

/** UpdateEmployeeDocumentDto (metadata only; the file itself can't be replaced). */
export interface UpdateDocumentRequest {
  documentType: DocumentType
  description: string | null
}
