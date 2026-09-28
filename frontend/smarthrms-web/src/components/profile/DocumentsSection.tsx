import { Download, Eye, FileText, Pencil, Trash2, Upload } from 'lucide-react'
import { useCallback, useState } from 'react'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import {
  canPreview,
  deactivateDocument,
  downloadDocument,
  getDocuments,
  updateDocument,
  uploadDocument,
} from '../../services/documentService'
import type { EmployeeDocument, UploadProgressHandler } from '../../types/document'
import { errorMessage } from '../../utils/errors'
import { enumLabel, formatDate, formatDateTime, formatFileSize, todayInput } from '../../utils/formatters'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { EmptyState } from '../common/EmptyState'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { Modal } from '../common/Modal'
import { Table, type Column } from '../common/Table'
import { DocumentForm, type DocumentFormResult } from './DocumentForm'
import { DocumentPreviewModal } from './DocumentPreviewModal'

type Editing = { document?: EmployeeDocument } | null

/** Days until the date, from today (negative = past). Dates are compared as calendar days. */
function daysUntil(date: string): number {
  const toDay = (value: string) => Date.UTC(Number(value.slice(0, 4)), Number(value.slice(5, 7)) - 1, Number(value.slice(8, 10)))
  return Math.round((toDay(date) - toDay(todayInput())) / 86_400_000)
}

function ExpiryCell({ expiryDate }: { expiryDate: string | null }) {
  if (!expiryDate) return <span className="text-base-content/50">—</span>
  const days = daysUntil(expiryDate)
  return (
    <div className="flex flex-col items-start gap-1">
      <span className="whitespace-nowrap">{formatDate(expiryDate)}</span>
      {days < 0 && <span className="badge badge-soft badge-error badge-xs">Expired</span>}
      {days >= 0 && days <= 30 && <span className="badge badge-soft badge-warning badge-xs">Expires soon</span>}
    </div>
  )
}

/** Employee documents: upload, list, view, download, edit metadata and deactivate (soft delete). */
export function DocumentsSection({ employeeId, onChanged }: { employeeId: string; onChanged: () => void }) {
  const { notify } = useToast()
  const [showInactive, setShowInactive] = useState(false)
  const [editing, setEditing] = useState<Editing>(null)
  const [previewing, setPreviewing] = useState<EmployeeDocument | null>(null)
  const [deactivating, setDeactivating] = useState<EmployeeDocument | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  const load = useCallback((signal: AbortSignal) => getDocuments(employeeId, showInactive, signal), [employeeId, showInactive])
  const { data: documents, error, loading, reload } = useApi(load)

  const refresh = () => {
    reload()
    onChanged()
  }

  const save = async ({ file, ...metadata }: DocumentFormResult, onProgress: UploadProgressHandler) => {
    if (editing?.document) {
      await updateDocument(employeeId, editing.document.id, metadata)
      notify('success', 'Document updated.')
    } else if (file) {
      await uploadDocument(employeeId, { ...metadata, file }, onProgress)
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

  const columns: Column<EmployeeDocument>[] = [
    {
      key: 'type',
      header: 'Document type',
      className: 'whitespace-nowrap',
      render: (document) => <span className="badge badge-ghost badge-sm">{enumLabel(document.documentType)}</span>,
    },
    {
      key: 'name',
      header: 'Document name',
      render: (document) => (
        <div className="min-w-44 max-w-72">
          <p className="font-medium">{document.documentName}</p>
          <p className="truncate text-xs text-base-content/60" title={document.fileName}>
            {document.fileName}
          </p>
          {document.description && <p className="mt-1 line-clamp-2 text-xs text-base-content/70">{document.description}</p>}
          {!document.isActive && <span className="badge badge-outline badge-xs mt-1">Deactivated</span>}
        </div>
      ),
    },
    { key: 'issue', header: 'Issue date', className: 'hidden md:table-cell whitespace-nowrap', render: (document) => formatDate(document.issueDate) },
    { key: 'expiry', header: 'Expiry date', className: 'hidden md:table-cell', render: (document) => <ExpiryCell expiryDate={document.expiryDate} /> },
    { key: 'size', header: 'File size', className: 'hidden lg:table-cell whitespace-nowrap', render: (document) => formatFileSize(document.fileSizeBytes) },
    {
      key: 'uploaded',
      header: 'Uploaded',
      className: 'hidden xl:table-cell whitespace-nowrap text-sm',
      render: (document) => formatDateTime(document.uploadedAt),
    },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (document) =>
        document.isActive && (
          <div className="flex justify-end gap-1">
            {canPreview(document) && (
              <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setPreviewing(document)} title="View" aria-label={`View ${document.documentName}`}>
                <Eye className="size-4" />
              </button>
            )}
            <button
              type="button"
              className="btn btn-ghost btn-xs btn-square"
              onClick={() => download(document)}
              disabled={downloadingId === document.id}
              title="Download"
              aria-label={`Download ${document.documentName}`}
            >
              {downloadingId === document.id ? <span className="loading loading-spinner loading-xs" /> : <Download className="size-4" />}
            </button>
            <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setEditing({ document })} title="Edit" aria-label={`Edit ${document.documentName}`}>
              <Pencil className="size-4" />
            </button>
            <button
              type="button"
              className="btn btn-ghost btn-xs btn-square text-error"
              onClick={() => setDeactivating(document)}
              title="Deactivate"
              aria-label={`Deactivate ${document.documentName}`}
            >
              <Trash2 className="size-4" />
            </button>
          </div>
        ),
    },
  ]

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
          <EmptyState
            compact
            icon={FileText}
            title={showInactive ? 'No documents' : 'No documents uploaded'}
            description="Upload NID, passport, certificates, CV and other HR documents."
          />
        ) : (
          <Table columns={columns} rows={documents} rowKey={(document) => document.id} />
        )}
      </div>

      <Modal open={editing !== null} title={editing?.document ? 'Edit document' : 'Upload document'} onClose={() => setEditing(null)} size="lg">
        {editing !== null && <DocumentForm initial={editing.document} onSubmit={save} onCancel={() => setEditing(null)} />}
      </Modal>

      {previewing && <DocumentPreviewModal document={previewing} onClose={() => setPreviewing(null)} onDownload={download} />}

      <ConfirmDialog
        open={deactivating !== null}
        title="Deactivate document?"
        message={`Are you sure you want to deactivate "${deactivating?.documentName}"? It will be removed from this employee's documents and can no longer be viewed or downloaded. The file is kept for HR records.`}
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
