/** Envelope returned by every SmartHRMS endpoint (ApiResponse<T> in the backend). */
export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T | null
  errors: string[] | null
}

/** A failed request, normalized from any HTTP or network error. `status` is null for network errors. */
export class ApiError extends Error {
  readonly status: number | null
  readonly errors: string[]

  constructor(message: string, status: number | null, errors: string[] = []) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}
