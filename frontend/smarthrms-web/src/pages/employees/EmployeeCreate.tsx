import { useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { EmployeeForm } from '../../components/employee/EmployeeForm'
import { toCreateRequest } from '../../components/employee/employeeFormSchema'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { getDepartments } from '../../services/departmentService'
import { getDesignations } from '../../services/designationService'
import { createEmployee, uploadEmployeePhoto } from '../../services/employeeService'
import { errorMessage } from '../../utils/errors'

export default function EmployeeCreate() {
  const navigate = useNavigate()
  const { notify } = useToast()
  const load = useCallback((signal: AbortSignal) => Promise.all([getDepartments(signal), getDesignations(signal)]), [])
  const { data, error, loading, reload } = useApi(load)

  return (
    <>
      <PageHeader title="Add employee" description="Create a new employee record." />
      {error ? (
        <div className="card bg-base-100 shadow-sm">
          <ErrorState error={error} onRetry={reload} />
        </div>
      ) : loading || !data ? (
        <Loading />
      ) : (
        <EmployeeForm
          mode="create"
          departments={data[0]}
          designations={data[1]}
          cancelTo="/employees"
          onSubmit={async (values, photo) => {
            const created = await createEmployee(toCreateRequest(values))
            if (photo) {
              try {
                await uploadEmployeePhoto(created.id, photo)
              } catch (uploadError) {
                // The employee exists now, so go to it rather than letting a retry create a duplicate.
                notify('error', `Employee created, but the photo was not uploaded: ${errorMessage(uploadError)}`)
                navigate(`/employees/${created.id}/edit`)
                return
              }
            }
            notify('success', `Employee ${created.fullName} created.`)
            navigate(`/employees/${created.id}`)
          }}
        />
      )}
    </>
  )
}
