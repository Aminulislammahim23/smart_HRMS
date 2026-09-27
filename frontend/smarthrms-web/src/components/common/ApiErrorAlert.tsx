import { AlertCircle } from 'lucide-react'
import { errorDetails, errorMessage } from '../../utils/errors'

/** Inline alert for a failed request, listing the backend's validation errors when there are several. */
export function ApiErrorAlert({ error }: { error: unknown }) {
  if (!error) return null
  const details = errorDetails(error)

  return (
    <div role="alert" className="alert alert-error alert-soft items-start">
      <AlertCircle className="size-5 shrink-0" />
      <div className="text-sm">
        <p className="font-medium">{errorMessage(error)}</p>
        {details.length > 0 && (
          <ul className="mt-1 list-disc pl-5">
            {details.map((detail) => (
              <li key={detail}>{detail}</li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
