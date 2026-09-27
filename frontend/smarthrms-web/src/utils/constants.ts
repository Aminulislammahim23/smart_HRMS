export const APP_NAME = 'SmartHRMS'

/** API origin without a trailing slash, e.g. http://localhost:5099. Empty = same origin (Vite then proxies /api and /uploads). */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

/** Mirrors EmployeePhotoPolicy in the backend (the backend stays the final authority). */
export const PHOTO_RULES = {
  maxSizeBytes: 5 * 1024 * 1024,
  extensions: ['.jpg', '.jpeg', '.png', '.webp'],
  accept: 'image/jpeg,image/png,image/webp',
} as const

/**
 * Mirrors the default EmployeeDocuments settings in the backend's appsettings.json. The server can be configured
 * differently; its error message is shown if it rejects a file these rules accept.
 */
export const DOCUMENT_RULES = {
  maxSizeBytes: 10 * 1024 * 1024,
  extensions: ['.pdf', '.doc', '.docx', '.jpg', '.jpeg', '.png'],
} as const

export const PAGE_SIZE_OPTIONS = [10, 25, 50] as const
