import { Menu } from 'lucide-react'
import { useAuth } from '../../hooks/useAuth'
import { Breadcrumb } from './Breadcrumb'

export function Topbar({ onMenuClick }: { onMenuClick: () => void }) {
  const { isAuthEnabled } = useAuth()

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b border-base-300 bg-base-100/90 px-4 backdrop-blur lg:px-6">
      <button type="button" className="btn btn-ghost btn-square btn-sm lg:hidden" onClick={onMenuClick} aria-label="Open menu">
        <Menu className="size-5" />
      </button>

      <Breadcrumb />

      {!isAuthEnabled && (
        <span className="badge badge-ghost ml-auto hidden md:inline-flex" title="The backend does not provide sign-in yet.">
          Sign-in not enabled
        </span>
      )}
    </header>
  )
}
