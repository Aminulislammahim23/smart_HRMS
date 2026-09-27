import type { ReactNode } from 'react'

interface FormFieldProps {
  label: string
  htmlFor: string
  error?: string
  hint?: string
  required?: boolean
  className?: string
  children: ReactNode
}

/** Label, control, and either the validation error or a hint underneath. */
export function FormField({ label, htmlFor, error, hint, required, className = '', children }: FormFieldProps) {
  return (
    <fieldset className={`fieldset py-0 ${className}`}>
      <label htmlFor={htmlFor} className="fieldset-legend pb-1 text-sm font-medium">
        {label}
        {required && <span className="text-error">*</span>}
      </label>
      {children}
      {error ? (
        <p className="label text-error whitespace-normal" role="alert">
          {error}
        </p>
      ) : (
        hint && <p className="label whitespace-normal">{hint}</p>
      )}
    </fieldset>
  )
}
