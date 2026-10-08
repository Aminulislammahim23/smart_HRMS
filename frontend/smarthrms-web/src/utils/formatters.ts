import type { BloodGroup } from '../types/profile'
import { API_BASE_URL } from './constants'

const BLOOD_GROUP_LABELS: Record<BloodGroup, string> = {
  APositive: 'A+',
  ANegative: 'A−',
  BPositive: 'B+',
  BNegative: 'B−',
  ABPositive: 'AB+',
  ABNegative: 'AB−',
  OPositive: 'O+',
  ONegative: 'O−',
}

const SPECIAL_LABELS: Record<string, string> = {
  Nid: 'NID',
  Cv: 'Resume / CV',
  TinCertificate: 'TIN certificate',
}

/** Turns an enum name into a label: "OnLeave" → "On leave", "FullTime" → "Full time", "APositive" → "A+". */
export function enumLabel(value: string | null | undefined): string {
  if (!value) return '—'
  if (value in BLOOD_GROUP_LABELS) return BLOOD_GROUP_LABELS[value as BloodGroup]
  if (value in SPECIAL_LABELS) return SPECIAL_LABELS[value]
  const words = value.replace(/([a-z])([A-Z])/g, '$1 $2').split(' ')
  return words.map((word, index) => (index === 0 ? word : word.toLowerCase())).join(' ')
}

/**
 * Date-only backend values (date of birth, joining date, ...) arrive as "yyyy-MM-ddT00:00:00" with no time zone.
 * They are read as calendar dates so no time zone can shift them by a day.
 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '—'
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return '—'
  return new Date(year, month - 1, day).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

/** Timestamps (createdAt, updatedAt, uploadedAt) are UTC instants; show them in local time. */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? '—'
    : date.toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

/** "yyyy-MM-dd" for <input type="date">. */
export function toDateInput(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : ''
}

/** Today's local date as "yyyy-MM-dd". */
export function todayInput(): string {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

/** The backend stores no currency, so salaries are shown as plain amounts. */
export function formatAmount(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

/** Absolute URL for a server-relative path such as an employee photo URL. */
export function assetUrl(path: string | null | undefined): string | null {
  if (!path) return null
  return /^https?:\/\//i.test(path) ? path : `${API_BASE_URL}${path}`
}

export function initials(firstName: string, lastName: string): string {
  return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase() || '?'
}

/** Trims a form value and turns a blank one into null (the backend treats both the same way). */
export function blankToNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? ''
  return trimmed === '' ? null : trimmed
}

/** "09:05:00" → "09:05" (attendance times are office wall-clock times; never time-zone converted). */
export function formatTime(value: string | null | undefined): string {
  return value ? value.slice(0, 5) : '—'
}

/** Working minutes → "8h 30m". */
export function formatDuration(minutes: number | null | undefined): string {
  if (minutes === null || minutes === undefined) return '—'
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return hours === 0 ? `${rest}m` : `${hours}h ${String(rest).padStart(2, '0')}m`
}

/** A Date's local calendar day as "yyyy-MM-dd". */
export function toIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** First and last day of a month ("yyyy-MM") as "yyyy-MM-dd". */
export function monthRange(month: string): { start: string; end: string } {
  const [year, monthIndex] = month.split('-').map(Number)
  return { start: toIsoDate(new Date(year, monthIndex - 1, 1)), end: toIsoDate(new Date(year, monthIndex, 0)) }
}

/** "2026-09" → "September 2026". */
export function formatMonth(month: string): string {
  const [year, monthIndex] = month.split('-').map(Number)
  return new Date(year, monthIndex - 1, 1).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
}

/** Moves a "yyyy-MM" month by n months. */
export function shiftMonth(month: string, n: number): string {
  const [year, monthIndex] = month.split('-').map(Number)
  return toIsoDate(new Date(year, monthIndex - 1 + n, 1)).slice(0, 7)
}

/** The local calendar date of a UTC instant (e.g. "processed on"), unlike formatDate which reads date-only values. */
export function formatInstantDate(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
