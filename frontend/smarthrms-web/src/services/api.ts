import axios, { type AxiosResponse } from 'axios'
import { ApiError, type ApiResponse } from '../types/api'
import { API_BASE_URL } from '../utils/constants'
import { tokenStore } from './tokenStore'

/** The single HTTP client for the SmartHRMS API. Every failure is rejected as an ApiError (except cancellations). */
export const api = axios.create({
  baseURL: `${API_BASE_URL}/api`,
  headers: { Accept: 'application/json' },
  timeout: 30_000,
})

// Every request carries the signed-in user's access token. The API decides what that user may do.
api.interceptors.request.use((config) => {
  const token = tokenStore.get()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    // Cancelled requests (e.g. a page unmounting) are not errors the user should see.
    if (axios.isCancel(error)) {
      return Promise.reject(error)
    }
    // An expired or revoked session (but not a failed sign-in attempt): end it so the app returns to the sign-in page.
    if (axios.isAxiosError(error) && error.response?.status === 401 && !isSignInRequest(error.config?.url)) {
      tokenStore.clear()
      tokenStore.notifyUnauthorized()
    }
    return Promise.reject(await toApiError(error))
  },
)

/** Unwraps the ApiResponse envelope and returns its data. */
export async function unwrap<T>(request: Promise<AxiosResponse<ApiResponse<T>>>): Promise<T> {
  const response = await request
  return response.data.data as T
}

/** Unwraps an envelope that carries no data and returns its message. */
export async function unwrapMessage(request: Promise<AxiosResponse<ApiResponse<unknown>>>): Promise<string> {
  const response = await request
  return response.data.message
}

function isSignInRequest(url: string | undefined): boolean {
  return !!url && url.replace(/^\/+/, '').startsWith('auth/login')
}

export function isCancelled(error: unknown): boolean {
  return axios.isCancel(error)
}

async function readEnvelope(data: unknown): Promise<Partial<ApiResponse<unknown>> | null> {
  // Downloads request a Blob, so an error body arrives as a Blob too.
  if (data instanceof Blob) {
    try {
      return JSON.parse(await data.text()) as Partial<ApiResponse<unknown>>
    } catch {
      return null
    }
  }
  return data && typeof data === 'object' ? (data as Partial<ApiResponse<unknown>>) : null
}

async function toApiError(error: unknown): Promise<ApiError> {
  if (!axios.isAxiosError(error)) {
    return new ApiError('An unexpected error occurred.', null)
  }

  if (!error.response) {
    if (import.meta.env.DEV) console.error('[api] network error', error.message)
    return error.code === 'ECONNABORTED'
      ? new ApiError('The server took too long to respond. Please try again.', null)
      : new ApiError('Cannot reach the SmartHRMS server. Check your connection and that the API is running.', null)
  }

  const { status } = error.response
  const body = await readEnvelope(error.response.data)
  const errors = Array.isArray(body?.errors) ? body.errors.filter((e): e is string => typeof e === 'string') : []
  const serverMessage = typeof body?.message === 'string' && body.message.trim() ? body.message : null

  // A gateway (the dev proxy, IIS, a load balancer) answering for an API that is down or restarting.
  if (status === 502 || status === 503 || status === 504) {
    if (import.meta.env.DEV) console.error('[api] server unavailable', status, error.config?.url)
    return new ApiError('The SmartHRMS server is unavailable right now. Please try again shortly.', status)
  }

  if (status >= 500) {
    if (import.meta.env.DEV) console.error('[api] server error', status, error.config?.url)
    return new ApiError('Something went wrong on the server. Please try again later.', status)
  }

  switch (status) {
    case 400:
      return new ApiError(serverMessage ?? 'The request was not valid.', status, errors)
    case 401:
      // A failed sign-in carries its own reason ("Invalid username or password.", "Too many failed sign-in attempts...").
      return isSignInRequest(error.config?.url) && errors.length > 0
        ? new ApiError(errors[0], status, errors)
        : new ApiError('You are not signed in or your session has expired.', status)
    case 403:
      return new ApiError('You do not have permission to do this.', status, errors)
    case 404:
      return new ApiError(serverMessage ?? 'The requested record was not found.', status, errors)
    case 409:
      return new ApiError(serverMessage ?? 'This conflicts with existing data.', status, errors)
    case 413:
      return new ApiError('The file is too large.', status)
    default:
      return new ApiError(serverMessage ?? `The request failed (HTTP ${status}).`, status, errors)
  }
}
