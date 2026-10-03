import { Link, useLocation } from 'react-router-dom'

const LABELS: Record<string, string> = {
  dashboard: 'Dashboard',
  employees: 'Employees',
  departments: 'Departments',
  designations: 'Designations',
  profile: 'My Profile',
  create: 'Create',
  edit: 'Edit',
  documents: 'Documents',
  attendance: 'Attendance',
  records: 'Records',
  leave: 'Leave',
  approvals: 'Approvals',
  payroll: 'Payroll',
  periods: 'Periods',
  salaries: 'Salary structures',
  my: 'My payroll',
  me: 'My attendance',
  approval: 'Approval',
  payslip: 'Payslip',
  users: 'Users & roles',
}

/** Label for an id segment, by the segment before it; ids of pages that have no own page are skipped. */
const ID_LABELS: Record<string, string> = {
  employees: 'Employee details',
  documents: 'Employee documents',
  employee: 'Employee attendance',
  payroll: 'Payroll period',
  payslip: 'Payslip',
}

/** Path segments that only group routes and have no page of their own. */
const SKIPPED_SEGMENTS = new Set(['employee', 'payslip'])

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

interface Crumb {
  label: string
  to: string
}

/** Builds the trail from the URL: /employees/{id}/edit → Employees › Employee details › Edit. */
function buildCrumbs(pathname: string): Crumb[] {
  const segments = pathname.split('/').filter(Boolean)
  const crumbs: Crumb[] = []
  let path = ''

  segments.forEach((segment, index) => {
    path += `/${segment}`
    if (GUID.test(segment)) {
      const previous = segments[index - 1]
      // /documents/{employeeId}/{documentId}: the second id is the document.
      const label = GUID.test(previous) && segments[0] === 'documents' ? 'Document' : ID_LABELS[previous]
      // Department/designation ids only appear in edit URLs and have no page of their own.
      if (label) crumbs.push({ label, to: path })
      return
    }
    if (SKIPPED_SEGMENTS.has(segment)) return
    crumbs.push({ label: LABELS[segment] ?? segment, to: path })
  })

  return crumbs
}

export function Breadcrumb() {
  const { pathname } = useLocation()
  const crumbs = buildCrumbs(pathname)

  return (
    <div className="breadcrumbs min-w-0 py-0 text-sm">
      <ul>
        {crumbs.map((crumb, index) => (
          <li key={crumb.to}>
            {index === crumbs.length - 1 ? (
              <span className="font-medium" aria-current="page">
                {crumb.label}
              </span>
            ) : (
              <Link to={crumb.to} className="text-base-content/60">
                {crumb.label}
              </Link>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
