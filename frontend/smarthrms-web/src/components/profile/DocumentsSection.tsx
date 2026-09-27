import { Download, FileText, Pencil, Trash2, Upload } from 'lucide-react'
import { useCallback, useState } from 'react'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { deactivateDocument, downloadDocument, getDocuments, updateDocument, uploadDocument } from '../../services/documentService'
import type { EmployeeDocument } from '../../types/document'
import { errorMessage } from '../../utils/errors'
import { enumLabel, formatDateTime, formatFileSize } from '../../utils/formatters'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { EmptyState } from '../common/EmptyState'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { Modal } from '../common/Modal'
import { DocumentForm, type DocumentFormResult } from './DocumentForm'

type Editing = { document?: EmployeeDocument } | null

/** Employee documents (Day 10): upload, download, edit type/description and deactivate. */
export function DocumentsSection({ employeeId, onChanged }: { employeeId: string; onChanged: () => void }) {
  const { notify } = useToast()
  const [showInactive, setShowInactive] = useState(false)
  const [editing, setEditing] = useState<Editing>(null)
  const [deactivating, setDeactivating] = useState<EmployeeDocument | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  const load = useCallback((signal: AbortSignal) => getDocuments(employeeId, showInactive, signal), [employeeId, showInactive])
  const { data: documents, error, loading, reload } = useApi(load)

  const refresh = () => {
    reload()
    onChanged()
  }

  const save = async ({ documentType, description, file }: DocumentFormResult) => {
    if (editing?.document) {
      await updateDocument(employeeId, editing.document.id, { documentType, description })
      notify('success', 'Document updated.')
    } else if (file) {
      await uploadDocument(employeeId, { file, documentType, description })
      notify('success', 'Document uploaded.')
    }
    setEditing(null)
    refresh()
  }

  const download = async (document: EmployeeDocument) => {
    setDownloadingId(document.id)
    try {
      await downloadDocument(document)
    } catch (caught) {
      notify('error', errorMessage(caught))
    } finally {
      setDownloadingId(null)
    }
  }

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="font-semibold">Documents</h2>
            <p className="text-sm text-base-content/60">NID, passport, certificates, CV, letters and contracts. Stored privately.</p>
          </div>
          <div className="flex items-center gap-3">
            <label className="label cursor-pointer gap-2 text-sm">
              <input type="checkbox" className="toggle toggle-sm" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
              Show deactivated
            </label>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setEditing({})}>
              <Upload className="size-4" /> Upload
            </button>
          </div>
        </div>

        {error ? (
          <ErrorState error={error} onRetry={reload} />
        ) : loading && !documents ? (
          <Loading label="Loading documents…" />
        ) : !documents || documents.length === 0 ? (
          <EmptyState compact icon={FileText} title="No documents uploaded" />
        ) : (
          <ul className="divide-y divide-base-300">
            {documents.map((document) => (
              <li key={document.id} className={`flex flex-col gap-3 py-3 sm:flex-row sm:items-center ${document.isActive ? '' : 'opacity-60'}`}>
                <div className="flex min-w-0 flex-1 items-start gap-3">
                  <div className="rounded-lg bg-primary/10 p-2 text-primary">
                    <FileText className="size-5" />
                  </div>
                  <div className="min-w-0">
                    <p className="truncate font-medium">{document.fileName}</p>
                    <p className="text-xs text-base-content/60">
                      <span className="badge badge-ghost badge-xs mr-1">{enumLabel(document.documentType)}</span>
                      {formatFileSize(document.fileSizeBytes)} · Uploaded {formatDateTime(document.uploadedAt)}
                      {!document.isActive && ' · Deactivated'}
                    </p>
                    {document.description && <p className="mt-1 text-sm text-base-content/80">{document.description}</p>}
                  </div>
                </div>
                {document.isActive && (
                  <div className="flex shrink-0 gap-1 self-end sm:self-center">
                    <button type="button" className="btn btn-ghost btn-xs" onClick={() => download(document)} disabled={downloadingId === document.id}>
                      {downloadingId === document.id ? <span className="loading loading-spinner loading-xs" /> : <Download className="size-3.5" />}
                      Download
                    </button>
                    <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setEditing({ document })} aria-label={`Edit ${document.fileName}`}>
                      <Pencil className="size-3.5" />
                    </button>
                    <button type="button" className="btn btn-ghost btn-xs btn-square text-error" onClick={() => setDeactivating(document)} aria-label={`Deactivate ${document.fileName}`}>
                      <Trash2 className="size-3.5" />
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      <Modal open={editing !== null} title={editing?.document ? 'Edit document' : 'Upload document'} onClose={() => setEditing(null)}>
        {editing !== null && <DocumentForm initial={editing.document} onSubmit={save} onCancel={() => setEditing(null)} />}
      </Modal>

      <ConfirmDialog
        open={deactivating !== null}
        title="Deactivate document?"
        message={`"${deactivating?.fileName}" will be hidden from the profile and can no longer be downloaded. The file is kept for HR records.`}
        confirmLabel="Deactivate"
        onCancel={() => setDeactivating(null)}
        onConfirm={async () => {
          if (!deactivating) return
          await deactivateDocument(employeeId, deactivating.id)
          notify('success', 'Document deactivated.')
          setDeactivating(null)
          refresh()
        }}
      />
    </div>
  )
}
