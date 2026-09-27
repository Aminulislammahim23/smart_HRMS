import { useCallback } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { EmployeeForm } from '../../components/employee/EmployeeForm'
import { toUpdateRequest } from '../../components/employee/employeeFormSchema'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { getDepartments } from '../../services/departmentService'
import { getDesignations } from '../../services/designationService'
import { getEmployeeById, removeEmployeePhoto, updateEmployee, uploadEmployeePhoto } from '../../services/employeeService'
import { errorMessage } from '../../utils/errors'

export default function EmployeeEdit() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { notify } = useToast()
  const load = useCallback(
    (signal: AbortSignal) => Promise.all([getEmployeeById(id, signal), getDepartments(signal), getDesignations(signal)]),
    [id],
  )
  const { data, error, loading, reload } = useApi(load)
  const employee = data?.[0]

  return (
    <>
      <PageHeader title="Edit employee" description={employee ? `${employee.fullName} · ${employee.employeeCode}` : undefined} />
      {error ? (
        <div className="card bg-base-100 shadow-sm">
          <ErrorState
            error={error}
            onRetry={reload}
            action={
              <Link to="/employees" className="btn btn-sm btn-ghost">
                Back to employees
              </Link>
            }
          />
        </div>
      ) : loading || !data || !employee ? (
        <Loading />
      ) : (
        <EmployeeForm
          key={employee.id}
          mode="edit"
          employee={employee}
          departments={data[1]}
          designations={data[2]}
          cancelTo={`/employees/${id}`}
          onRemovePhoto={async () => {
            await removeEmployeePhoto(id)
            notify('success', 'Photo removed.')
          }}
          onSubmit={async (values, photo) => {
            const updated = await updateEmployee(id, toUpdateRequest(values))
            if (photo) {
              try {
                await uploadEmployeePhoto(id, photo)
              } catch (uploadError) {
                notify('error', `Changes saved, but the photo was not uploaded: ${errorMessage(uploadError)}`)
                navigate(`/employees/${id}`)
                return
              }
            }
            notify('success', `Employee ${updated.fullName} updated.`)
            navigate(`/employees/${id}`)
          }}
        />
      )}
    </>
  )
}
