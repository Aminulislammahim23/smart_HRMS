import { Info, Pencil, UserCircle } from 'lucide-react'
import { useCallback } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { EmployeeProfileView } from '../../components/profile/EmployeeProfileView'
import { useApi } from '../../hooks/useApi'
import { getEmployeeProfile, getEmployees } from '../../services/employeeService'

function ProfileContent({ employeeId }: { employeeId: string }) {
  const load = useCallback((signal: AbortSignal) => getEmployeeProfile(employeeId, signal), [employeeId])
  const { data: profile, error, loading, reload } = useApi(load)

  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} /></div>
  if (loading && !profile) return <Loading label="Loading profile…" />
  if (!profile) return null

  return (
    <EmployeeProfileView
      profile={profile}
      onChanged={reload}
      actions={
        <Link to={`/employees/${employeeId}/edit`} className="btn btn-sm btn-primary">
          <Pencil className="size-4" /> Edit employee record
        </Link>
      }
    />
  )
}

/**
 * "My profile" needs to know who is signed in, and the backend has no authentication yet. Until it does, the
 * profile to show is chosen here; the page then uses the same Day 11 profile APIs a signed-in employee would.
 */
export default function MyProfile() {
  const [params, setParams] = useSearchParams()
  const employeeId = params.get('employeeId') ?? ''
  const { data: employees, error, loading, reload } = useApi(getEmployees)

  return (
    <>
      <PageHeader title="My Profile" description="Personal details, addresses, contacts, education, experience and documents." />

      <div role="note" className="alert alert-info alert-soft mb-6 items-start">
        <Info className="size-5 shrink-0" />
        <div className="flex w-full flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <span className="text-sm">
            Sign-in is not available in the backend yet, so the app can't tell which employee you are. Choose the employee
            whose profile to open.
          </span>
          {error ? (
            <button type="button" className="btn btn-sm" onClick={reload}>
              Retry loading employees
            </button>
          ) : (
            <select
              className="select select-sm w-full md:w-72"
              value={employeeId}
              disabled={loading && !employees}
              onChange={(event) => setParams(event.target.value ? { employeeId: event.target.value } : {}, { replace: true })}
              aria-label="Employee"
            >
              <option value="">{loading && !employees ? 'Loading employees…' : 'Select an employee'}</option>
              {employees
                ?.slice()
                .sort((a, b) => a.fullName.localeCompare(b.fullName))
                .map((employee) => (
                  <option key={employee.id} value={employee.id}>
                    {employee.fullName} ({employee.employeeCode})
                  </option>
                ))}
            </select>
          )}
        </div>
      </div>

      {employeeId ? (
        <ProfileContent key={employeeId} employeeId={employeeId} />
      ) : (
        <div className="card bg-base-100 shadow-sm">
          <EmptyState icon={UserCircle} title="No profile selected" description="Pick an employee above to view and manage their profile." />
        </div>
      )}
    </>
  )
}
