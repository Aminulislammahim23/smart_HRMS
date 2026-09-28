/** EmployeeDocumentType names, in the order the upload form lists them. */
export const DOCUMENT_TYPES = [
  'Nid',
  'Passport',
  'BirthCertificate',
  'EducationalCertificate',
  'ExperienceCertificate',
  'JoiningLetter',
  'Cv',
  'TinCertificate',
  'ContractPaper',
  'Other',
] as const
export type DocumentType = (typeof DOCUMENT_TYPES)[number]

/** EmployeeDocumentDto */
export interface EmployeeDocument {
  id: string
  employeeId: string
  documentType: DocumentType
  /** Title given by HR, e.g. "National ID card". */
  documentName: string
  issueDate: string | null
  expiryDate: string | null
  /** Original (sanitized) file name, used as the download name. */
  fileName: string
  contentType: string
  fileSizeBytes: number
  description: string | null
  /** False once deactivated (soft delete): hidden by default and no longer downloadable. */
  isActive: boolean
  uploadedAt: string
  updatedAt: string | null
  /** API route that streams the file. */
  downloadUrl: string
}

/** Metadata shared by upload and update. Dates are yyyy-MM-dd. */
export interface DocumentMetadata {
  documentType: DocumentType
  documentName: string
  issueDate: string | null
  expiryDate: string | null
  description: string | null
}

/** Multipart form for POST /api/employees/{id}/documents */
export interface UploadDocumentRequest extends DocumentMetadata {
  file: File
}

/** UpdateEmployeeDocumentDto (metadata only; the file itself can't be replaced). */
export type UpdateDocumentRequest = DocumentMetadata

/** Upload progress, 0–100. */
export type UploadProgressHandler = (percent: number) => void
