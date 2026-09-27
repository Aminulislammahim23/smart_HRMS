import {
  BriefcaseBusiness,
  Building2,
  LayoutDashboard,
  LogOut,
  Settings,
  UserCircle,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { APP_NAME } from '../../utils/constants'

interface NavItem {
  label: string
  to: string
  icon: LucideIcon
}

interface NavSection {
  title: string
  items: NavItem[]
}

// Add future HRMS modules (attendance, leave, payroll, ...) as new sections or items here.
const NAV_SECTIONS: NavSection[] = [
  { title: 'Overview', items: [{ label: 'Dashboard', to: '/dashboard', icon: LayoutDashboard }] },
  {
    title: 'People',
    items: [
      { label: 'Employees', to: '/employees', icon: Users },
      { label: 'My Profile', to: '/profile', icon: UserCircle },
    ],
  },
  {
    title: 'Organization',
    items: [
      { label: 'Departments', to: '/departments', icon: Building2 },
      { label: 'Designations', to: '/designations', icon: BriefcaseBusiness },
    ],
  },
]

export function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const { isAuthEnabled } = useAuth()

  return (
    <aside className="flex min-h-full w-64 flex-col border-r border-base-300 bg-base-100">
      <div className="flex h-16 items-center gap-2 border-b border-base-300 px-5">
        <div className="grid size-8 place-items-center rounded-lg bg-primary text-primary-content font-bold">S</div>
        <span className="text-lg font-semibold tracking-tight">{APP_NAME}</span>
      </div>

      <nav className="flex-1 overflow-y-auto p-3" aria-label="Main">
        {NAV_SECTIONS.map((section) => (
          <div key={section.title} className="mb-4">
            <p className="px-3 pb-1 text-xs font-semibold uppercase tracking-wider text-base-content/50">{section.title}</p>
            <ul className="menu w-full p-0">
              {section.items.map(({ label, to, icon: Icon }) => (
                <li key={to}>
                  <NavLink to={to} onClick={onNavigate} className={({ isActive }) => (isActive ? 'menu-active bg-primary/10! font-medium text-primary!' : '')}>
                    <Icon className="size-4" />
                    {label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <ul className="menu w-full border-t border-base-300 p-3">
        <li className="menu-disabled">
          <span title="Settings are not available yet.">
            <Settings className="size-4" />
            Settings
            <span className="badge badge-ghost badge-xs">Soon</span>
          </span>
        </li>
        <li className={isAuthEnabled ? '' : 'menu-disabled'}>
          <span title={isAuthEnabled ? undefined : 'Sign-in is not available until the backend adds authentication.'}>
            <LogOut className="size-4" />
            Logout
          </span>
        </li>
      </ul>
    </aside>
  )
}
