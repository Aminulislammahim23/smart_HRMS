import { useState } from 'react'
import { useToast } from '../../hooks/useToast'
import { updateDocument, uploadDocument } from '../../services/documentService'
import { ApiError } from '../../types/api'
import type { EmployeeDocument, UploadProgressHandler } from '../../types/document'
import type { Employee } from '../../types/employee'
import { FormField } from '../common/FormField'
import { Modal } from '../common/Modal'
import { DocumentForm, type DocumentFormResult } from '../profile/DocumentForm'

interface DocumentUploadModalProps {
  open: boolean
  onClose: () => void
  onSaved: () => void
  /** Edit this document's metadata instead of uploading a new one. */
  document?: EmployeeDocument
  /** Upload for this employee. Without it, the user picks one from `employees`. */
  employeeId?: string
  employees?: Employee[]
}

/** Upload a new document (POST multipart) or edit an existing one's metadata (PUT). */
export function DocumentUploadModal({ open, onClose, onSaved, document, employeeId, employees = [] }: DocumentUploadModalProps) {
  const { notify } = useToast()
  const [chosenEmployeeId, setChosenEmployeeId] = useState(employeeId ?? '')
  const [employeeError, setEmployeeError] = useState<string | null>(null)
  const targetEmployeeId = document?.employeeId ?? employeeId ?? chosenEmployeeId

  const save = async ({ file, ...metadata }: DocumentFormResult, onProgress: UploadProgressHandler) => {
    if (!targetEmployeeId) {
      setEmployeeError('Select an employee.')
      throw new ApiError('Select the employee this document belongs to.', null)
    }
    if (document) {
      await updateDocument(document.employeeId, document.id, metadata)
      notify('success', 'Document updated.')
    } else if (file) {
      await uploadDocument(targetEmployeeId, { ...metadata, file }, onProgress)
      notify('success', 'Document uploaded.')
    }
    onClose()
    onSaved()
  }

  return (
    <Modal open={open} title={document ? 'Edit document' : 'Upload document'} onClose={onClose} size="lg">
      {open && (
        <>
          {!document && !employeeId && (
            <div className="mb-4">
              <FormField label="Employee" htmlFor="documentEmployee" error={employeeError ?? undefined} required>
                <select
                  id="documentEmployee"
                  className="select w-full"
                  value={chosenEmployeeId}
                  onChange={(e) => {
                    setChosenEmployeeId(e.target.value)
                    setEmployeeError(null)
                  }}
                >
                  <option value="">Select an employee</option>
                  {employees.map((employee) => (
                    <option key={employee.id} value={employee.id}>
                      {employee.fullName} ({employee.employeeCode})
                    </option>
                  ))}
                </select>
              </FormField>
            </div>
          )}
          <DocumentForm initial={document} onSubmit={save} onCancel={onClose} />
        </>
      )}
    </Modal>
  )
}
