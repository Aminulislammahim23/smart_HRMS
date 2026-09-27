import { ArrowLeft, Pencil, UserX } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ConfirmDialog } from '../../components/common/ConfirmDialog'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { EmployeeProfileView } from '../../components/profile/EmployeeProfileView'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { deactivateEmployee, getEmployeeProfile } from '../../services/employeeService'

export default function EmployeeDetails() {
  const { id = '' } = useParams()
  const { notify } = useToast()
  const [confirmDeactivate, setConfirmDeactivate] = useState(false)
  const load = useCallback((signal: AbortSignal) => getEmployeeProfile(id, signal), [id])
  const { data: profile, error, loading, reload } = useApi(load)

  if (error) {
    return (
      <div className="card bg-base-100 shadow-sm">
        <ErrorState
          error={error}
          onRetry={reload}
          action={
            <Link to="/employees" className="btn btn-sm btn-ghost">
              <ArrowLeft className="size-4" /> Back to employees
            </Link>
          }
        />
      </div>
    )
  }

  if (loading && !profile) return <Loading label="Loading employee…" />
  if (!profile) return null

  return (
    <>
      <EmployeeProfileView
        profile={profile}
        onChanged={reload}
        actions={
          <>
            <Link to="/employees" className="btn btn-sm btn-ghost">
              <ArrowLeft className="size-4" /> Back
            </Link>
            <Link to={`/employees/${id}/edit`} className="btn btn-sm btn-primary">
              <Pencil className="size-4" /> Edit
            </Link>
            {profile.jobInformation.employmentStatus !== 'Inactive' && (
              <button type="button" className="btn btn-sm btn-ghost text-error" onClick={() => setConfirmDeactivate(true)}>
                <UserX className="size-4" /> Deactivate
              </button>
            )}
          </>
        }
      />

      <ConfirmDialog
        open={confirmDeactivate}
        title="Deactivate employee?"
        message={`${profile.fullName} will be marked Inactive. The record and profile are kept and the employee can be reactivated from the edit page.`}
        confirmLabel="Deactivate"
        onCancel={() => setConfirmDeactivate(false)}
        onConfirm={async () => {
          const message = await deactivateEmployee(id)
          notify('success', message)
          setConfirmDeactivate(false)
          reload()
        }}
      />
    </>
  )
}
