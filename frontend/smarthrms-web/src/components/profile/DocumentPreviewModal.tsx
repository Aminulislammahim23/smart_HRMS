import { Download } from 'lucide-react'
import { useCallback, useEffect, useMemo } from 'react'
import { useApi } from '../../hooks/useApi'
import { fetchDocumentFile } from '../../services/documentService'
import type { EmployeeDocument } from '../../types/document'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { Modal } from '../common/Modal'

interface DocumentPreviewModalProps {
  document: EmployeeDocument
  onClose: () => void
  onDownload: (document: EmployeeDocument) => void
}

/** Shows a PDF or image in the browser. The file comes through the authenticated-ready API download endpoint. */
export function DocumentPreviewModal({ document, onClose, onDownload }: DocumentPreviewModalProps) {
  const load = useCallback(() => fetchDocumentFile(document), [document])
  const { data: blob, error, loading, reload } = useApi(load)

  // Object URLs pin the blob in memory, so release each one when it is replaced or the dialog closes.
  const url = useMemo(() => (blob ? URL.createObjectURL(blob) : null), [blob])
  useEffect(() => () => {
    if (url) URL.revokeObjectURL(url)
  }, [url])

  const isPdf = document.contentType === 'application/pdf'

  return (
    <Modal open title={document.documentName} onClose={onClose} size="lg">
      {error ? (
        <ErrorState error={error} onRetry={reload} />
      ) : loading || !url ? (
        <Loading label="Loading document…" />
      ) : isPdf ? (
        <iframe src={url} title={document.documentName} className="h-[70vh] w-full rounded-box border border-base-300 bg-white" />
      ) : (
        <div className="flex max-h-[70vh] justify-center overflow-auto rounded-box border border-base-300 bg-base-200">
          <img src={url} alt={document.documentName} className="max-w-full object-contain" />
        </div>
      )}
      <div className="modal-action">
        <button type="button" className="btn btn-ghost" onClick={onClose}>
          Close
        </button>
        <button type="button" className="btn btn-primary" onClick={() => onDownload(document)}>
          <Download className="size-4" /> Download
        </button>
      </div>
    </Modal>
  )
}
