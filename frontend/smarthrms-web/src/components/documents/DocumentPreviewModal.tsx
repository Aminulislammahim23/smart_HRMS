import { Download } from 'lucide-react'
import type { EmployeeDocument } from '../../types/document'
import { Modal } from '../common/Modal'
import { DocumentPreview } from './DocumentPreview'

interface DocumentPreviewModalProps {
  document: EmployeeDocument
  onClose: () => void
  onDownload: (document: EmployeeDocument) => void
}

/** PDF or image preview in a dialog, with a download button. */
export function DocumentPreviewModal({ document, onClose, onDownload }: DocumentPreviewModalProps) {
  return (
    <Modal open title={document.documentName} onClose={onClose} size="lg">
      <DocumentPreview document={document} />
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
