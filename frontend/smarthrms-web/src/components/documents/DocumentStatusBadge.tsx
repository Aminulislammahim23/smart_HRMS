import type { EmployeeDocument } from '../../types/document'
import { documentStatus, type DocumentStatus } from './documentStatus'

const STYLES: Record<DocumentStatus, { label: string; className: string }> = {
  Deactivated: { label: 'Deactivated', className: 'badge-outline' },
  Expired: { label: 'Expired', className: 'badge-soft badge-error' },
  ExpiresSoon: { label: 'Expires soon', className: 'badge-soft badge-warning' },
  Valid: { label: 'Active', className: 'badge-soft badge-success' },
}

/** Active / Expires soon / Expired / Deactivated. `hideValid` keeps busy tables quiet for normal documents. */
export function DocumentStatusBadge({ document, hideValid = false }: { document: EmployeeDocument; hideValid?: boolean }) {
  const status = documentStatus(document)
  if (hideValid && status === 'Valid') return null
  const { label, className } = STYLES[status]
  return <span className={`badge badge-xs whitespace-nowrap ${className}`}>{label}</span>
}
