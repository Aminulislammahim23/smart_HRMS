import { RefreshCw, ServerCrash } from 'lucide-react'
import type { ReactNode } from 'react'
import { ApiError } from '../../types/api'
import { errorMessage } from '../../utils/errors'

interface ErrorStateProps {
  error: unknown
  onRetry?: () => void
  action?: ReactNode
}

/** Full-section error for a failed page load, with a retry button. */
export function ErrorState({ error, onRetry, action }: ErrorStateProps) {
  const isNotFound = error instanceof ApiError && error.status === 404

  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-center" role="alert">
      <div className="rounded-full bg-error/10 p-3 text-error">
        <ServerCrash className="size-6" />
      </div>
      <p className="font-medium">{isNotFound ? 'Not found' : 'Could not load this page'}</p>
      <p className="max-w-md text-sm text-base-content/70">{errorMessage(error)}</p>
      <div className="mt-2 flex gap-2">
        {onRetry && !isNotFound && (
          <button type="button" className="btn btn-sm" onClick={onRetry}>
            <RefreshCw className="size-4" /> Try again
          </button>
        )}
        {action}
      </div>
    </div>
  )
}
