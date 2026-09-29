import type { EmployeeDocument } from '../../types/document'
import { todayInput } from '../../utils/formatters'

export type DocumentStatus = 'Deactivated' | 'Expired' | 'ExpiresSoon' | 'Valid'

/** Days from today to the date (negative = past), compared as calendar days. */
function daysUntil(date: string): number {
  const toDay = (value: string) => Date.UTC(Number(value.slice(0, 4)), Number(value.slice(5, 7)) - 1, Number(value.slice(8, 10)))
  return Math.round((toDay(date) - toDay(todayInput())) / 86_400_000)
}

/** Derived from the backend's isActive and expiryDate; "expires soon" means within 30 days. */
export function documentStatus(document: EmployeeDocument): DocumentStatus {
  if (!document.isActive) return 'Deactivated'
  if (!document.expiryDate) return 'Valid'
  const days = daysUntil(document.expiryDate)
  if (days < 0) return 'Expired'
  return days <= 30 ? 'ExpiresSoon' : 'Valid'
}
