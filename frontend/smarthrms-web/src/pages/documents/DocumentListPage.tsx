import { FileText, Upload } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { DocumentFilters, type DocumentFilterValues } from '../../components/documents/DocumentFilters'
import { DocumentTable } from '../../components/documents/DocumentTable'
import { DocumentUploadModal } from '../../components/documents/DocumentUploadModal'
import { EMPTY_DOCUMENT_FILTERS, filterDocuments } from '../../components/documents/filterDocuments'
import { useDocumentActions } from '../../components/documents/useDocumentActions'
import { useApi } from '../../hooks/useApi'
import { usePagination } from '../../hooks/usePagination'
import { getDocumentsForEmployees } from '../../services/documentService'
import { getEmployees } from '../../services/employeeService'

/**
 * Documents of all employees. The API lists documents per employee only, so this page loads the employee list and
 * then each employee's documents in parallel; search and filters are applied in the browser.
 */
export default function DocumentListPage() {
  const [filters, setFilters] = useState<DocumentFilterValues>(EMPTY_DOCUMENT_FILTERS)
  const [uploading, setUploading] = useState(false)
  const { showInactive, employeeId } = filters

  const load = useCallback(
    async (signal: AbortSignal) => {
      const employees = await getEmployees(signal)
      // With an employee selected, only that employee's documents are requested.
      const ids = employeeId ? [employeeId] : employees.map((employee) => employee.id)
      return { employees, documents: await getDocumentsForEmployees(ids, showInactive, signal) }
    },
    [showInactive, employeeId],
  )
  const { data, error, loading, reload } = useApi(load)
  const employees = useMemo(() => data?.employees ?? [], [data])
  const employeesById = useMemo(() => new Map(employees.map((employee) => [employee.id, employee])), [employees])

  const { actions, dialogs } = useDocumentActions(reload)
  const visible = useMemo(() => filterDocuments(data?.documents ?? [], filters, employeesById), [data, filters, employeesById])
  const pagination = usePagination(visible, 25)

  return (
    <>
      <PageHeader
        title="Documents"
        description="HR documents of all employees, stored privately."
        actions={
          <button type="button" className="btn btn-primary btn-sm" onClick={() => setUploading(true)}>
            <Upload className="size-4" /> Upload document
          </button>
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <DocumentFilters
            values={filters}
            employees={employees}
            onChange={(patch) => {
              setFilters((current) => ({ ...current, ...patch }))
              pagination.setPage(1)
            }}
          />

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label="Loading documents…" />
          ) : visible.length === 0 ? (
            <EmptyState
              icon={FileText}
              title={data?.documents.length ? 'No documents match your filters' : 'No documents uploaded yet'}
              description={data?.documents.length ? 'Try another search, employee or document type.' : 'Upload the first document to get started.'}
            />
          ) : (
            <>
              <p className="text-sm text-base-content/60">{visible.length} documents</p>
              <DocumentTable documents={pagination.pageItems} actions={actions} employeesById={employeesById} />
              <Pagination
                page={pagination.page}
                pageCount={pagination.pageCount}
                pageSize={pagination.pageSize}
                total={pagination.total}
                onPageChange={pagination.setPage}
                onPageSizeChange={pagination.setPageSize}
              />
            </>
          )}
        </div>
      </div>

      <DocumentUploadModal
        key={String(uploading)}
        open={uploading}
        employeeId={employeeId || undefined}
        employees={employees.filter((employee) => employee.isActive)}
        onClose={() => setUploading(false)}
        onSaved={reload}
      />
      {dialogs}
    </>
  )
}
