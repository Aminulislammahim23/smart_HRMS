import { X } from 'lucide-react'
import { useEffect, useId, type ReactNode } from 'react'

interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  size?: 'md' | 'lg'
  /** Blocks closing (Escape, backdrop, ×) while a request is running. */
  busy?: boolean
}

export function Modal({ open, title, onClose, children, size = 'md', busy = false }: ModalProps) {
  const titleId = useId()

  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !busy) onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [open, busy, onClose])

  if (!open) return null

  return (
    <div className="modal modal-open modal-bottom sm:modal-middle" role="dialog" aria-modal="true" aria-labelledby={titleId}>
      <div className={`modal-box ${size === 'lg' ? 'sm:max-w-3xl' : 'sm:max-w-lg'}`}>
        <div className="mb-4 flex items-center justify-between gap-4">
          <h3 id={titleId} className="text-lg font-semibold">
            {title}
          </h3>
          <button type="button" className="btn btn-ghost btn-sm btn-circle" onClick={onClose} disabled={busy} aria-label="Close">
            <X className="size-4" />
          </button>
        </div>
        {children}
      </div>
      <button type="button" className="modal-backdrop" onClick={() => !busy && onClose()} aria-label="Close dialog" tabIndex={-1} />
    </div>
  )
}
