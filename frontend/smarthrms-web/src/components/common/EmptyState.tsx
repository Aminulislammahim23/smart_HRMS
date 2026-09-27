import { Inbox, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

interface EmptyStateProps {
  title: string
  description?: string
  icon?: LucideIcon
  action?: ReactNode
  compact?: boolean
}

export function EmptyState({ title, description, icon: Icon = Inbox, action, compact = false }: EmptyStateProps) {
  return (
    <div className={`flex flex-col items-center justify-center gap-2 text-center ${compact ? 'py-8' : 'py-16'}`}>
      <div className="rounded-full bg-base-200 p-3 text-base-content/50">
        <Icon className="size-6" />
      </div>
      <p className="font-medium">{title}</p>
      {description && <p className="max-w-md text-sm text-base-content/60">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}
