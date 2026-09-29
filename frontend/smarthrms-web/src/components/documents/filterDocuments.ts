import type { EmployeeDocument } from '../../types/document'
import type { Employee } from '../../types/employee'
import type { DocumentFilterValues } from './DocumentFilters'

export const EMPTY_DOCUMENT_FILTERS: DocumentFilterValues = { search: '', employeeId: '', documentType: '', showInactive: false }

/**
 * Search and type/employee filters, applied in the browser: the documents API only lists one employee's documents
 * (optionally including deactivated ones) and has no search or type parameters.
 */
export function filterDocuments(
  documents: readonly EmployeeDocument[],
  filters: DocumentFilterValues,
  employeesById?: ReadonlyMap<string, Employee>,
): EmployeeDocument[] {
  const term = filters.search.trim().toLowerCase()
  return documents.filter((document) => {
    if (filters.employeeId && document.employeeId !== filters.employeeId) return false
    if (filters.documentType && document.documentType !== filters.documentType) return false
    if (!term) return true
    const employee = employeesById?.get(document.employeeId)
    return [document.documentName, document.fileName, document.description ?? '', employee?.fullName ?? '', employee?.employeeCode ?? '']
      .some((value) => value.toLowerCase().includes(term))
  })
}
