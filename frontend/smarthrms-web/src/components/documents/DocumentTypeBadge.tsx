import type { DocumentType } from '../../types/document'
import { enumLabel } from '../../utils/formatters'

export function DocumentTypeBadge({ type }: { type: DocumentType }) {
  return <span className="badge badge-ghost badge-sm whitespace-nowrap">{enumLabel(type)}</span>
}
