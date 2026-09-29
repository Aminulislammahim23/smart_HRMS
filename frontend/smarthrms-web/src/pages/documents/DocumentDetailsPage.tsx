import { ArrowLeft, Download, Pencil, Trash2 } from 'lucide-react'
import { useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { DetailList } from '../../components/common/DetailList'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { DocumentPreview } from '../../components/documents/DocumentPreview'
import { DocumentStatusBadge } from '../../components/documents/DocumentStatusBadge'
import { DocumentTypeBadge } from '../../components/documents/DocumentTypeBadge'
import { useDocumentActions } from '../../components/documents/useDocumentActions'
import { useApi } from '../../hooks/useApi'
import { canPreview, getDocument } from '../../services/documentService'
import { getEmployeeById } from '../../services/employeeService'
import { formatDate, formatDateTime, formatFileSize } from '../../utils/formatters'

/** Metadata of one document, with an inline preview for PDFs and images. */
export default function DocumentDetailsPage() {
  const { employeeId = '', documentId = '' } = useParams()
  const load = useCallback(
    (signal: AbortSignal) => Promise.all([getDocument(employeeId, documentId, signal), getEmployeeById(employeeId, signal)]),
    [employeeId, documentId],
  )
  const { data, error, loading, reload } = useApi(load)
  const { actions, dialogs } = useDocumentActions(reload)

  const back = (
    <Link to={`/documents/${employeeId}`} className="btn btn-sm btn-ghost">
      <ArrowLeft className="size-4" /> Employee documents
    </Link>
  )

  if (error) {
    return (
      <div className="card bg-base-100 shadow-sm">
        <ErrorState error={error} onRetry={reload} action={back} />
      </div>
    )
  }
  if (loading && !data) return <Loading label="Loading document…" />
  if (!data) return null
  const [document, employee] = data

  return (
    <div className="flex flex-col gap-6">
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-xl font-semibold">{document.documentName}</h1>
                <DocumentTypeBadge type={document.documentType} />
                <DocumentStatusBadge document={document} />
              </div>
              <p className="text-sm text-base-content/60">
                <Link to={`/employees/${employee.id}`} className="hover:text-primary">
                  {employee.fullName} ({employee.employeeCode})
                </Link>
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              {back}
              {document.isActive && (
                <>
                  <button type="button" className="btn btn-sm" onClick={() => actions.download(document)} disabled={actions.downloadingId === document.id}>
                    {actions.downloadingId === document.id ? <span className="loading loading-spinner loading-xs" /> : <Download className="size-4" />} Download
                  </button>
                  <button type="button" className="btn btn-sm" onClick={() => actions.edit(document)}>
                    <Pencil className="size-4" /> Edit
                  </button>
                  <button type="button" className="btn btn-sm btn-ghost text-error" onClick={() => actions.deactivate(document)}>
                    <Trash2 className="size-4" /> Deactivate
                  </button>
                </>
              )}
            </div>
          </div>

          <DetailList
            columns={3}
            items={[
              { label: 'File name', value: document.fileName },
              { label: 'File size', value: formatFileSize(document.fileSizeBytes) },
              { label: 'File type', value: document.contentType },
              { label: 'Issue date', value: formatDate(document.issueDate) },
              { label: 'Expiry date', value: formatDate(document.expiryDate) },
              { label: 'Uploaded', value: formatDateTime(document.uploadedAt) },
              { label: 'Last updated', value: formatDateTime(document.updatedAt) },
              { label: 'Description', value: document.description },
            ]}
          />
        </div>
      </div>

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-3">
          <h2 className="font-semibold">Preview</h2>
          {!document.isActive ? (
            <p className="text-sm text-base-content/60">This document is deactivated and can no longer be viewed or downloaded. Its record is kept for HR history.</p>
          ) : canPreview(document) ? (
            <DocumentPreview document={document} height="h-[75vh]" />
          ) : (
            <p className="text-sm text-base-content/60">Word documents can't be shown in the browser. Use Download to open the file.</p>
          )}
        </div>
      </div>

      {dialogs}
    </div>
  )
}
