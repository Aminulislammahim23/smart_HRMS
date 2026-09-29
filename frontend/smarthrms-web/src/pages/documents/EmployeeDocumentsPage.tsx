import { ArrowLeft, FileText, Upload } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { DocumentFilters, type DocumentFilterValues } from '../../components/documents/DocumentFilters'
import { DocumentTable } from '../../components/documents/DocumentTable'
import { DocumentUploadModal } from '../../components/documents/DocumentUploadModal'
import { EMPTY_DOCUMENT_FILTERS, filterDocuments } from '../../components/documents/filterDocuments'
import { useDocumentActions } from '../../components/documents/useDocumentActions'
import { EmployeePhoto } from '../../components/employee/EmployeePhoto'
import { useApi } from '../../hooks/useApi'
import { getDocuments } from '../../services/documentService'
import { getEmployeeById } from '../../services/employeeService'

/** All documents of one employee. */
export default function EmployeeDocumentsPage() {
  const { employeeId = '' } = useParams()
  const [filters, setFilters] = useState<DocumentFilterValues>(EMPTY_DOCUMENT_FILTERS)
  const [uploading, setUploading] = useState(false)

  const load = useCallback(
    (signal: AbortSignal) => Promise.all([getEmployeeById(employeeId, signal), getDocuments(employeeId, filters.showInactive, signal)]),
    [employeeId, filters.showInactive],
  )
  const { data, error, loading, reload } = useApi(load)
  const [employee, documents] = data ?? [undefined, []]

  const { actions, dialogs } = useDocumentActions(reload)
  const visible = useMemo(() => filterDocuments(documents, filters), [documents, filters])

  if (error) {
    return (
      <div className="card bg-base-100 shadow-sm">
        <ErrorState
          error={error}
          onRetry={reload}
          action={
            <Link to="/documents" className="btn btn-sm btn-ghost">
              <ArrowLeft className="size-4" /> All documents
            </Link>
          }
        />
      </div>
    )
  }
  if (loading && !data) return <Loading label="Loading documents…" />
  if (!employee) return null

  return (
    <div className="flex flex-col gap-6">
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body flex-col gap-4 sm:flex-row sm:items-center">
          <EmployeePhoto photoUrl={employee.photoUrl} firstName={employee.firstName} lastName={employee.lastName} size="lg" />
          <div className="flex-1">
            <h1 className="text-xl font-semibold">{employee.fullName} — documents</h1>
            <p className="text-sm text-base-content/60">
              {employee.employeeCode} · {employee.designationName} · {employee.departmentName}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link to="/documents" className="btn btn-sm btn-ghost">
              <ArrowLeft className="size-4" /> All documents
            </Link>
            <Link to={`/employees/${employee.id}`} className="btn btn-sm">
              Employee profile
            </Link>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setUploading(true)}>
              <Upload className="size-4" /> Upload
            </button>
          </div>
        </div>
      </div>

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <DocumentFilters values={filters} onChange={(patch) => setFilters((current) => ({ ...current, ...patch }))} />
          {visible.length === 0 ? (
            <EmptyState icon={FileText} title={documents.length ? 'No documents match your filters' : 'No documents uploaded'} />
          ) : (
            <DocumentTable documents={visible} actions={actions} />
          )}
        </div>
      </div>

      <DocumentUploadModal key={String(uploading)} open={uploading} employeeId={employee.id} onClose={() => setUploading(false)} onSaved={reload} />
      {dialogs}
    </div>
  )
}
