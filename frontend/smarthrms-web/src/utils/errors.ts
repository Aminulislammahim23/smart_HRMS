import { ApiError } from '../types/api'

/**
 * User-friendly message for any error thrown by a service call. For 400/404/409 the backend sends a generic title
 * ("Conflict.") in `message` and the actual reason in `errors`, so a single reason is preferred over the title.
 */
export function errorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return 'An unexpected error occurred.'
  return error.errors.length === 1 ? error.errors[0] : error.message
}

/** Extra details worth listing under errorMessage(): only when there are several (e.g. model validation). */
export function errorDetails(error: unknown): string[] {
  return error instanceof ApiError && error.errors.length > 1 ? error.errors : []
}
