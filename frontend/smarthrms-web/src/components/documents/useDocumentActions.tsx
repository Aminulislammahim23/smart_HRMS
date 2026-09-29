import { useState, type ReactNode } from 'react'
import { useToast } from '../../hooks/useToast'
import { deactivateDocument, downloadDocument } from '../../services/documentService'
import type { EmployeeDocument } from '../../types/document'
import { errorMessage } from '../../utils/errors'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { DocumentPreviewModal } from './DocumentPreviewModal'
import { DocumentUploadModal } from './DocumentUploadModal'

export interface DocumentActions {
  preview: (document: EmployeeDocument) => void
  download: (document: EmployeeDocument) => Promise<void>
  edit: (document: EmployeeDocument) => void
  deactivate: (document: EmployeeDocument) => void
  downloadingId: string | null
}

/**
 * Preview, download, edit-metadata and deactivate (soft delete) for any list of documents, with the dialogs they
 * need. Render `dialogs` once in the page.
 */
export function useDocumentActions(onChanged: () => void): { actions: DocumentActions; dialogs: ReactNode } {
  const { notify } = useToast()
  const [previewing, setPreviewing] = useState<EmployeeDocument | null>(null)
  const [editing, setEditing] = useState<EmployeeDocument | null>(null)
  const [deactivating, setDeactivating] = useState<EmployeeDocument | null>(null)
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

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

  const dialogs = (
    <>
      {previewing && <DocumentPreviewModal document={previewing} onClose={() => setPreviewing(null)} onDownload={download} />}
      <DocumentUploadModal key={editing?.id ?? 'none'} open={editing !== null} document={editing ?? undefined} onClose={() => setEditing(null)} onSaved={onChanged} />
      <ConfirmDialog
        open={deactivating !== null}
        title="Deactivate document?"
        message={`Are you sure you want to deactivate "${deactivating?.documentName}"? It will be removed from this employee's documents and can no longer be viewed or downloaded. The file is kept for HR records.`}
        confirmLabel="Deactivate"
        onCancel={() => setDeactivating(null)}
        onConfirm={async () => {
          if (!deactivating) return
          await deactivateDocument(deactivating.employeeId, deactivating.id)
          notify('success', 'Document deactivated.')
          setDeactivating(null)
          onChanged()
        }}
      />
    </>
  )

  return {
    actions: { preview: setPreviewing, download, edit: setEditing, deactivate: setDeactivating, downloadingId },
    dialogs,
  }
}
