import { CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { ToastContext, type ToastType } from './toastContext'

interface Toast {
  id: number
  type: ToastType
  message: string
}

const STYLES: Record<ToastType, { className: string; Icon: typeof Info }> = {
  success: { className: 'alert-success', Icon: CheckCircle2 },
  error: { className: 'alert-error', Icon: XCircle },
  info: { className: 'alert-info', Icon: Info },
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(0)

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const notify = useCallback(
    (type: ToastType, message: string) => {
      const id = ++nextId.current
      setToasts((current) => [...current.slice(-3), { id, type, message }])
      window.setTimeout(() => dismiss(id), type === 'error' ? 7000 : 4000)
    },
    [dismiss],
  )

  const value = useMemo(() => ({ notify }), [notify])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast toast-end toast-bottom z-[100]" aria-live="polite">
        {toasts.map(({ id, type, message }) => {
          const { className, Icon } = STYLES[type]
          return (
            <div key={id} role={type === 'error' ? 'alert' : 'status'} className={`alert ${className} max-w-sm shadow-lg`}>
              <Icon className="size-5 shrink-0" />
              <span className="text-sm whitespace-normal">{message}</span>
              <button type="button" className="btn btn-ghost btn-xs btn-circle" onClick={() => dismiss(id)} aria-label="Dismiss">
                <X className="size-4" />
              </button>
            </div>
          )
        })}
      </div>
    </ToastContext.Provider>
  )
}
