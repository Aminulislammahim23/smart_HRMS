import {
  Banknote,
  BarChart3,
  BriefcaseBusiness,
  Building2,
  CalendarCheck,
  CalendarDays,
  ClipboardCheck,
  FileStack,
  FileText,
  HandCoins,
  History,
  Landmark,
  LayoutDashboard,
  LogOut,
  ReceiptText,
  ScrollText,
  ShieldCheck,
  UserCircle,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import type { AuthUser, UserRole } from '../../types/auth'
import { APP_NAME } from '../../utils/constants'

interface NavItem {
  label: string
  to: string
  icon: LucideIcon
  /** Roles that see the item; omitted = everyone signed in. */
  roles?: UserRole[]
  /** Only for accounts linked to an employee (self-service pages). */
  requireEmployee?: boolean
  /** Exact match for the active state (a parent route that has child pages of its own). */
  end?: boolean
}

interface NavSection {
  title: string
  items: NavItem[]
}

const HR: UserRole[] = ['HR', 'Admin']

// Navigation only hides what a role can't use; the API enforces the same rules on every request.
const NAV_SECTIONS: NavSection[] = [
  { title: 'Overview', items: [{ label: 'Dashboard', to: '/dashboard', icon: LayoutDashboard }] },
  {
    title: 'My workspace',
    items: [
      { label: 'My Profile', to: '/profile', icon: UserCircle, requireEmployee: true },
      { label: 'My Attendance', to: '/attendance/me', icon: CalendarCheck, requireEmployee: true },
      { label: 'My Leave', to: '/leave', icon: CalendarDays, requireEmployee: true, end: true },
      { label: 'My Payroll', to: '/payroll/my', icon: ReceiptText, requireEmployee: true },
    ],
  },
  {
    title: 'People',
    items: [
      { label: 'Employees', to: '/employees', icon: Users, roles: HR },
      { label: 'Attendance', to: '/attendance', icon: CalendarCheck, roles: HR, end: true },
      { label: 'Leave approvals', to: '/leave/approvals', icon: ClipboardCheck, roles: ['Manager', 'HR', 'Admin'] },
      { label: 'Documents', to: '/documents', icon: FileText, roles: HR },
    ],
  },
  {
    title: 'Payroll',
    items: [
      { label: 'Payroll dashboard', to: '/payroll', icon: Wallet, roles: HR, end: true },
      { label: 'Payroll periods', to: '/payroll/periods', icon: Landmark, roles: HR },
      { label: 'Payroll history', to: '/payroll/history', icon: History, roles: HR },
      { label: 'Payslips', to: '/payroll/payslips', icon: FileStack, roles: HR, end: true },
      { label: 'Payment batches', to: '/payments/batches', icon: Banknote, roles: HR },
      { label: 'Payment history', to: '/payments/history', icon: HandCoins, roles: HR },
      { label: 'Payroll reports', to: '/payroll/reports', icon: BarChart3, roles: HR },
      { label: 'Salary structures', to: '/payroll/salaries', icon: ReceiptText, roles: HR },
    ],
  },
  {
    title: 'Organization',
    items: [
      { label: 'Departments', to: '/departments', icon: Building2, roles: HR },
      { label: 'Designations', to: '/designations', icon: BriefcaseBusiness, roles: HR },
    ],
  },
  {
    title: 'Administration',
    items: [
      { label: 'Users & roles', to: '/users', icon: ShieldCheck, roles: ['Admin'] },
      { label: 'Payroll audit trail', to: '/payroll/audit', icon: ScrollText, roles: ['Admin'] },
    ],
  },
]

function visible(item: NavItem, user: AuthUser | null): boolean {
  if (!user) return false
  if (item.roles && !item.roles.includes(user.role)) return false
  return !item.requireEmployee || !!user.employeeId
}

export function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const { user, logout } = useAuth()
  const sections = NAV_SECTIONS.map((section) => ({ ...section, items: section.items.filter((item) => visible(item, user)) })).filter(
    (section) => section.items.length > 0,
  )

  return (
    <aside className="flex min-h-full w-64 flex-col border-r border-base-300 bg-base-100">
      <div className="flex h-16 items-center gap-2 border-b border-base-300 px-5">
        <div className="grid size-8 place-items-center rounded-lg bg-primary text-primary-content font-bold">S</div>
        <span className="text-lg font-semibold tracking-tight">{APP_NAME}</span>
      </div>

      <nav className="flex-1 overflow-y-auto p-3" aria-label="Main">
        {sections.map((section) => (
          <div key={section.title} className="mb-4">
            <p className="px-3 pb-1 text-xs font-semibold uppercase tracking-wider text-base-content/50">{section.title}</p>
            <ul className="menu w-full p-0">
              {section.items.map(({ label, to, icon: Icon, end }) => (
                <li key={to}>
                  <NavLink
                    to={to}
                    end={end}
                    onClick={onNavigate}
                    className={({ isActive }) => (isActive ? 'menu-active bg-primary/10! font-medium text-primary!' : '')}
                  >
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
        <li>
          <button type="button" onClick={() => logout()}>
            <LogOut className="size-4" />
            Sign out
          </button>
        </li>
      </ul>
    </aside>
  )
}
