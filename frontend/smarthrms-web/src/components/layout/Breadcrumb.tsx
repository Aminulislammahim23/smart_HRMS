import { Link, useLocation } from 'react-router-dom'

const LABELS: Record<string, string> = {
  dashboard: 'Dashboard',
  employees: 'Employees',
  departments: 'Departments',
  designations: 'Designations',
  profile: 'My Profile',
  create: 'Create',
  edit: 'Edit',
}

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
      // Only employees have a details page; department/designation ids only appear in edit URLs.
      if (segments[index - 1] === 'employees') crumbs.push({ label: 'Employee details', to: path })
      return
    }
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
