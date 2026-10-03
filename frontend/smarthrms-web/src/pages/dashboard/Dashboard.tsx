import { BriefcaseBusiness, Building2, CalendarClock, UserCheck, UserPlus, Users, type LucideIcon } from 'lucide-react'
import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { EmployeeStatusBadge } from '../../components/common/StatusBadge'
import { EmployeePhoto } from '../../components/employee/EmployeePhoto'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { getDepartments } from '../../services/departmentService'
import { getDesignations } from '../../services/designationService'
import { getEmployees } from '../../services/employeeService'
import { EMPLOYEE_STATUSES } from '../../types/employee'
import { enumLabel, formatDate } from '../../utils/formatters'
import EmployeeHome from './EmployeeHome'

interface StatCardProps {
  label: string
  value: number
  hint: string
  icon: LucideIcon
  to: string
}

function StatCard({ label, value, hint, icon: Icon, to }: StatCardProps) {
  return (
    <Link to={to} className="card bg-base-100 shadow-sm transition hover:shadow-md">
      <div className="card-body flex-row items-center gap-4 p-5">
        <div className="rounded-xl bg-primary/10 p-3 text-primary">
          <Icon className="size-6" />
        </div>
        <div className="min-w-0">
          <p className="text-sm text-base-content/60">{label}</p>
          <p className="text-2xl font-semibold">{value}</p>
          <p className="truncate text-xs text-base-content/50">{hint}</p>
        </div>
      </div>
    </Link>
  )
}

/** HR and Admin get the workforce overview; everyone else gets their own workspace. */
export default function Dashboard() {
  const { hasRole } = useAuth()
  return hasRole('HR', 'Admin') ? <WorkforceDashboard /> : <EmployeeHome />
}

/** Everything here is computed from the live employee, department and designation lists. */
function WorkforceDashboard() {
  const load = useCallback(
    (signal: AbortSignal) => Promise.all([getEmployees(signal), getDepartments(signal), getDesignations(signal)]),
    [],
  )
  const { data, error, loading, reload } = useApi(load)

  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} /></div>
  if (loading || !data) return <Loading label="Loading dashboard…" />

  const [employees, departments, designations] = data
  const current = employees.filter((employee) => employee.isActive)
  const onLeave = employees.filter((employee) => employee.status === 'OnLeave').length
  const activeDepartments = departments.filter((department) => department.isActive)
  const recentJoiners = [...employees].sort((a, b) => b.joiningDate.localeCompare(a.joiningDate)).slice(0, 5)

  const headcount = activeDepartments
    .map((department) => ({
      department,
      count: current.filter((employee) => employee.departmentId === department.id).length,
    }))
    .filter((row) => row.count > 0)
    .sort((a, b) => b.count - a.count)
  const maxHeadcount = Math.max(1, ...headcount.map((row) => row.count))

  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Workforce overview."
        actions={
          <Link to="/employees/create" className="btn btn-primary btn-sm">
            <UserPlus className="size-4" /> Add employee
          </Link>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Total employees" value={employees.length} hint="All records, any status" icon={Users} to="/employees" />
        <StatCard label="Currently working" value={current.length} hint={`${onLeave} on leave`} icon={UserCheck} to="/employees?status=current" />
        <StatCard
          label="Departments"
          value={activeDepartments.length}
          hint={`${departments.length - activeDepartments.length} inactive`}
          icon={Building2}
          to="/departments"
        />
        <StatCard
          label="Designations"
          value={designations.filter((designation) => designation.isActive).length}
          hint={`${designations.filter((designation) => !designation.isActive).length} inactive`}
          icon={BriefcaseBusiness}
          to="/designations"
        />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-5">
        <div className="card bg-base-100 shadow-sm lg:col-span-3">
          <div className="card-body">
            <div className="flex items-center justify-between">
              <h2 className="font-semibold">Recent joiners</h2>
              <Link to="/employees" className="link link-primary text-sm">
                View all
              </Link>
            </div>
            {recentJoiners.length === 0 ? (
              <EmptyState compact icon={CalendarClock} title="No employees yet" />
            ) : (
              <ul className="divide-y divide-base-300">
                {recentJoiners.map((employee) => (
                  <li key={employee.id}>
                    <Link to={`/employees/${employee.id}`} className="flex items-center gap-3 py-3 hover:text-primary">
                      <EmployeePhoto photoUrl={employee.photoUrl} firstName={employee.firstName} lastName={employee.lastName} size="sm" />
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-medium">{employee.fullName}</p>
                        <p className="truncate text-xs text-base-content/60">
                          {employee.designationName} · {employee.departmentName}
                        </p>
                      </div>
                      <div className="hidden text-right text-xs text-base-content/60 sm:block">{formatDate(employee.joiningDate)}</div>
                      <EmployeeStatusBadge status={employee.status} />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        <div className="flex flex-col gap-6 lg:col-span-2">
          <div className="card bg-base-100 shadow-sm">
            <div className="card-body">
              <h2 className="font-semibold">Headcount by department</h2>
              <p className="text-xs text-base-content/60">Currently working employees.</p>
              {headcount.length === 0 ? (
                <EmptyState compact icon={Building2} title="No assignments yet" />
              ) : (
                <ul className="mt-2 flex flex-col gap-3">
                  {headcount.map(({ department, count }) => (
                    <li key={department.id}>
                      <div className="mb-1 flex justify-between text-sm">
                        <Link to={`/employees?department=${department.id}`} className="truncate hover:text-primary">
                          {department.name}
                        </Link>
                        <span className="font-medium">{count}</span>
                      </div>
                      <progress className="progress progress-primary w-full" value={count} max={maxHeadcount} />
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>

          <div className="card bg-base-100 shadow-sm">
            <div className="card-body">
              <h2 className="font-semibold">Employees by status</h2>
              <ul className="mt-2 flex flex-col gap-2">
                {EMPLOYEE_STATUSES.map((status) => (
                  <li key={status} className="flex items-center justify-between text-sm">
                    <Link to={`/employees?status=${status}`} className="hover:text-primary">
                      {enumLabel(status)}
                    </Link>
                    <span className="font-medium">{employees.filter((employee) => employee.status === status).length}</span>
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      </div>
    </>
  )
}
