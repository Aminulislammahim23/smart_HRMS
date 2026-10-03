import { Pencil } from 'lucide-react'
import { useCallback } from 'react'
import { Link } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { EmployeeProfileView } from '../../components/profile/EmployeeProfileView'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { getEmployeeProfile } from '../../services/employeeService'

/**
 * The signed-in user's own profile. Employees and Managers see it read-only (profile records are maintained by HR;
 * the API refuses changes from anyone else). The route only opens for accounts linked to an employee.
 */
export default function MyProfile() {
  const { user, hasRole } = useAuth()
  const employeeId = user?.employeeId ?? ''
  const canEdit = hasRole('HR', 'Admin')

  const load = useCallback((signal: AbortSignal) => getEmployeeProfile(employeeId, signal), [employeeId])
  const { data: profile, error, loading, reload } = useApi(load)

  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} /></div>
  if (loading && !profile) return <Loading label="Loading your profile…" />
  if (!profile) return null

  return (
    <>
      {!canEdit && (
        <div role="note" className="alert alert-info alert-soft mb-6">
          <span className="text-sm">Your profile is maintained by HR. Contact HR if something needs to change.</span>
        </div>
      )}
      <EmployeeProfileView
        profile={profile}
        onChanged={reload}
        readOnly={!canEdit}
        actions={
          canEdit && (
            <Link to={`/employees/${employeeId}/edit`} className="btn btn-sm btn-primary">
              <Pencil className="size-4" /> Edit employee record
            </Link>
          )
        }
      />
    </>
  )
}
