import { createContext } from 'react'

export type ToastType = 'success' | 'error' | 'info'

export interface ToastApi {
  notify: (type: ToastType, message: string) => void
}

export const ToastContext = createContext<ToastApi | null>(null)
