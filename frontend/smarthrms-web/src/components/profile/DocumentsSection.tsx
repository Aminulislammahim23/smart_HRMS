import { ExternalLink, FileText, Upload } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link } from 'react-router-dom'
import { useApi } from '../../hooks/useApi'
import { getDocuments } from '../../services/documentService'
import { EmptyState } from '../common/EmptyState'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { DocumentFilters, type DocumentFilterValues } from '../documents/DocumentFilters'
import { DocumentTable } from '../documents/DocumentTable'
import { DocumentUploadModal } from '../documents/DocumentUploadModal'
import { EMPTY_DOCUMENT_FILTERS, filterDocuments } from '../documents/filterDocuments'
import { useDocumentActions } from '../documents/useDocumentActions'

/** Documents tab of the employee profile: the shared document list for one employee. */
export function DocumentsSection({ employeeId, onChanged }: { employeeId: string; onChanged: () => void }) {
  const [filters, setFilters] = useState<DocumentFilterValues>(EMPTY_DOCUMENT_FILTERS)
  const [uploading, setUploading] = useState(false)

  const load = useCallback((signal: AbortSignal) => getDocuments(employeeId, filters.showInactive, signal), [employeeId, filters.showInactive])
  const { data: documents, error, loading, reload } = useApi(load)

  const refresh = () => {
    reload()
    onChanged()
  }
  const { actions, dialogs } = useDocumentActions(refresh)
  const visible = filterDocuments(documents ?? [], filters)

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="font-semibold">Documents</h2>
            <p className="text-sm text-base-content/60">NID, passport, certificates, CV, letters and contracts. Stored privately.</p>
          </div>
          <div className="flex items-center gap-2">
            <Link to={`/documents/${employeeId}`} className="btn btn-ghost btn-sm">
              <ExternalLink className="size-4" /> Open in Documents
            </Link>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setUploading(true)}>
              <Upload className="size-4" /> Upload
            </button>
          </div>
        </div>

        <DocumentFilters values={filters} onChange={(patch) => setFilters((current) => ({ ...current, ...patch }))} />

        {error ? (
          <ErrorState error={error} onRetry={reload} />
        ) : loading && !documents ? (
          <Loading label="Loading documents…" />
        ) : visible.length === 0 ? (
          <EmptyState
            compact
            icon={FileText}
            title={documents?.length ? 'No documents match your filters' : filters.showInactive ? 'No documents' : 'No documents uploaded'}
            description="Upload NID, passport, certificates, CV and other HR documents."
          />
        ) : (
          <DocumentTable documents={visible} actions={actions} />
        )}
      </div>

      <DocumentUploadModal key={String(uploading)} open={uploading} employeeId={employeeId} onClose={() => setUploading(false)} onSaved={refresh} />
      {dialogs}
    </div>
  )
}
