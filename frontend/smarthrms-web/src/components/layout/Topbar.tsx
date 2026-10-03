import { KeyRound, LogOut, Menu } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '../../hooks/useAuth'
import { Breadcrumb } from './Breadcrumb'
import { ChangePasswordModal } from './ChangePasswordModal'

export function Topbar({ onMenuClick }: { onMenuClick: () => void }) {
  const { user, logout } = useAuth()
  const [changingPassword, setChangingPassword] = useState(false)
  const initials = (user?.displayName ?? '?')
    .split(/\s+/)
    .map((part) => part.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase()

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b border-base-300 bg-base-100/90 px-4 backdrop-blur lg:px-6 print:hidden">
      <button type="button" className="btn btn-ghost btn-square btn-sm lg:hidden" onClick={onMenuClick} aria-label="Open menu">
        <Menu className="size-5" />
      </button>

      <Breadcrumb />

      {user && (
        <div className="dropdown dropdown-end ml-auto">
          <button type="button" tabIndex={0} className="btn btn-ghost btn-sm gap-2 px-2" aria-label="Account menu">
            <span className="grid size-8 place-items-center rounded-full bg-primary/15 text-xs font-semibold text-primary">{initials}</span>
            <span className="hidden text-left leading-tight md:block">
              <span className="block text-sm font-medium">{user.displayName}</span>
              <span className="block text-xs text-base-content/60">{user.role}</span>
            </span>
          </button>
          <ul tabIndex={0} className="menu dropdown-content z-40 mt-2 w-56 rounded-box border border-base-300 bg-base-100 p-2 shadow-lg">
            <li className="menu-title">
              <span className="truncate">{user.username}</span>
            </li>
            <li>
              <button type="button" onClick={() => setChangingPassword(true)}>
                <KeyRound className="size-4" /> Change password
              </button>
            </li>
            <li>
              <button type="button" onClick={() => logout()}>
                <LogOut className="size-4" /> Sign out
              </button>
            </li>
          </ul>
        </div>
      )}

      {changingPassword && <ChangePasswordModal open onClose={() => setChangingPassword(false)} />}
    </header>
  )
}
