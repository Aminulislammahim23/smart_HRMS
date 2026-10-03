import { Download, Eye, FileSearch, Pencil, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import { canPreview } from '../../services/documentService'
import type { EmployeeDocument } from '../../types/document'
import type { Employee } from '../../types/employee'
import { formatDate, formatDateTime, formatFileSize } from '../../utils/formatters'
import { Table, type Column } from '../common/Table'
import { DocumentStatusBadge } from './DocumentStatusBadge'
import { DocumentTypeBadge } from './DocumentTypeBadge'
import type { DocumentActions } from './useDocumentActions'

interface DocumentTableProps {
  documents: readonly EmployeeDocument[]
  actions: DocumentActions
  /** When given, an Employee column is shown (cross-employee lists). */
  employeesById?: ReadonlyMap<string, Employee>
  /** False hides edit and deactivate (read-only viewers such as an employee looking at their own documents). */
  canManage?: boolean
}

export function DocumentTable({ documents, actions, employeesById, canManage = true }: DocumentTableProps) {
  // The Employee column needs room: on cross-employee lists the less important columns appear only on wide
  // screens, and below xl the document type moves into the name cell.
  const crossEmployee = employeesById !== undefined
  const columns: Column<EmployeeDocument>[] = [
    {
      key: 'type',
      header: 'Document type',
      className: `whitespace-nowrap ${crossEmployee ? 'hidden xl:table-cell' : ''}`,
      render: (d) => <DocumentTypeBadge type={d.documentType} />,
    },
    {
      key: 'name',
      header: 'Document name',
      render: (d) => (
        <div className="min-w-44 max-w-72">
          <Link to={`/documents/${d.employeeId}/${d.id}`} className="font-medium hover:text-primary">
            {d.documentName}
          </Link>
          <p className="truncate text-xs text-base-content/60" title={d.fileName}>
            {d.fileName}
          </p>
          {d.description && <p className="mt-1 line-clamp-2 text-xs text-base-content/70">{d.description}</p>}
          <div className="mt-1 flex flex-wrap gap-1">
            {crossEmployee && (
              <span className="xl:hidden">
                <DocumentTypeBadge type={d.documentType} />
              </span>
            )}
            <DocumentStatusBadge document={d} hideValid />
          </div>
        </div>
      ),
    },
    ...(employeesById
      ? [
          {
            key: 'employee',
            header: 'Employee',
            render: (d: EmployeeDocument) => {
              const employee = employeesById.get(d.employeeId)
              return employee ? (
                <Link to={`/documents/${employee.id}`} className="block min-w-36 hover:text-primary">
                  <span className="font-medium">{employee.fullName}</span>
                  <span className="block text-xs text-base-content/60">{employee.employeeCode}</span>
                </Link>
              ) : (
                '—'
              )
            },
          },
        ]
      : []),
    { key: 'issue', header: 'Issue date', className: `hidden ${crossEmployee ? 'xl:table-cell' : 'md:table-cell'} whitespace-nowrap`, render: (d) => formatDate(d.issueDate) },
    { key: 'expiry', header: 'Expiry date', className: 'hidden md:table-cell whitespace-nowrap', render: (d) => formatDate(d.expiryDate) },
    { key: 'size', header: 'File size', className: `hidden ${crossEmployee ? '2xl:table-cell' : 'lg:table-cell'} whitespace-nowrap`, render: (d) => formatFileSize(d.fileSizeBytes) },
    { key: 'uploaded', header: 'Uploaded', className: `hidden ${crossEmployee ? '2xl:table-cell' : 'xl:table-cell'} whitespace-nowrap text-sm`, render: (d) => formatDateTime(d.uploadedAt) },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (d) => (
        <div className="flex justify-end gap-1">
          <Link to={`/documents/${d.employeeId}/${d.id}`} className="btn btn-ghost btn-xs btn-square" title="Details" aria-label={`Details of ${d.documentName}`}>
            <FileSearch className="size-4" />
          </Link>
          {d.isActive && (
            <>
              {canPreview(d) && (
                <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => actions.preview(d)} title="View" aria-label={`View ${d.documentName}`}>
                  <Eye className="size-4" />
                </button>
              )}
              <button
                type="button"
                className="btn btn-ghost btn-xs btn-square"
                onClick={() => actions.download(d)}
                disabled={actions.downloadingId === d.id}
                title="Download"
                aria-label={`Download ${d.documentName}`}
              >
                {actions.downloadingId === d.id ? <span className="loading loading-spinner loading-xs" /> : <Download className="size-4" />}
              </button>
              {canManage && (
                <>
                  <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => actions.edit(d)} title="Edit" aria-label={`Edit ${d.documentName}`}>
                    <Pencil className="size-4" />
                  </button>
                  <button type="button" className="btn btn-ghost btn-xs btn-square text-error" onClick={() => actions.deactivate(d)} title="Deactivate" aria-label={`Deactivate ${d.documentName}`}>
                    <Trash2 className="size-4" />
                  </button>
                </>
              )}
            </>
          )}
        </div>
      ),
    },
  ]

  return <Table columns={columns} rows={documents} rowKey={(d) => d.id} />
}
