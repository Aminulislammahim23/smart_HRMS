import { zodResolver } from '@hookform/resolvers/zod'
import { Save } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { Link } from 'react-router-dom'
import type { Department } from '../../types/department'
import type { Designation } from '../../types/designation'
import { EMPLOYEE_STATUSES, EMPLOYMENT_TYPES, type Employee } from '../../types/employee'
import { enumLabel, todayInput } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormField } from '../common/FormField'
import { EmployeePhotoField } from './EmployeePhotoField'
import { employeeFormSchema, toFormValues, type EmployeeFormMode, type EmployeeFormValues } from './employeeFormSchema'

interface EmployeeFormProps {
  mode: EmployeeFormMode
  employee?: Employee
  departments: Department[]
  designations: Designation[]
  /** Saves the employee, then uploads the photo if one was chosen. Throw to show the error on the form. */
  onSubmit: (values: EmployeeFormValues, photo: File | null) => Promise<void>
  onRemovePhoto?: () => Promise<void>
  cancelTo: string
}

interface Option {
  id: string
  label: string
}

/** Active entries, plus the employee's current one even if it has since been deactivated (the backend allows keeping it). */
function assignable(units: (Department | Designation)[], currentId?: string): Option[] {
  return units
    .filter((unit) => unit.isActive || unit.id === currentId)
    .map((unit) => ({ id: unit.id, label: unit.isActive ? unit.name : `${unit.name} (inactive)` }))
}

function Section({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div>
          <h2 className="font-semibold">{title}</h2>
          <p className="text-sm text-base-content/60">{description}</p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">{children}</div>
      </div>
    </div>
  )
}

export function EmployeeForm({ mode, employee, departments, designations, onSubmit, onRemovePhoto, cancelTo }: EmployeeFormProps) {
  const [photo, setPhoto] = useState<File | null>(null)
  const [submitError, setSubmitError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<EmployeeFormValues>({
    resolver: zodResolver(employeeFormSchema(mode)),
    defaultValues: toFormValues(employee),
  })
  const [firstName, lastName] = useWatch({ control, name: ['firstName', 'lastName'] })

  const departmentOptions = assignable(departments, employee?.departmentId)
  const designationOptions = assignable(designations, employee?.designationId)

  const submit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await onSubmit(values, photo)
    } catch (error) {
      setSubmitError(error)
      window.scrollTo({ top: 0, behavior: 'smooth' })
    }
  })

  return (
    <form onSubmit={submit} noValidate className="grid gap-6 lg:grid-cols-3">
      <div className="flex flex-col gap-6 lg:col-span-2">
        <ApiErrorAlert error={submitError} />

        <Section title="Basic information" description="Identity and contact details.">
          <FormField
            label="Employee code"
            htmlFor="employeeCode"
            error={errors.employeeCode?.message}
            hint={mode === 'edit' ? 'The code cannot be changed.' : 'Unique, e.g. EMP-0012.'}
            required={mode === 'create'}
          >
            <input id="employeeCode" className="input w-full" disabled={mode === 'edit'} {...register('employeeCode')} />
          </FormField>
          <FormField label="Email" htmlFor="email" error={errors.email?.message} required>
            <input id="email" type="email" className="input w-full" autoComplete="off" {...register('email')} />
          </FormField>
          <FormField label="First name" htmlFor="firstName" error={errors.firstName?.message} required>
            <input id="firstName" className="input w-full" {...register('firstName')} />
          </FormField>
          <FormField label="Last name" htmlFor="lastName" error={errors.lastName?.message} required>
            <input id="lastName" className="input w-full" {...register('lastName')} />
          </FormField>
          <FormField label="Phone" htmlFor="phone" error={errors.phone?.message} hint="Optional.">
            <input id="phone" type="tel" className="input w-full" placeholder="+8801XXXXXXXXX" {...register('phone')} />
          </FormField>
          <FormField label="Date of birth" htmlFor="dateOfBirth" error={errors.dateOfBirth?.message} required>
            <input id="dateOfBirth" type="date" className="input w-full" max={todayInput()} {...register('dateOfBirth')} />
          </FormField>
        </Section>

        <Section title="Employment" description="Position, contract and pay.">
          <FormField
            label="Department"
            htmlFor="departmentId"
            error={errors.departmentId?.message}
            hint={departmentOptions.length === 0 ? 'No active departments. Create one first.' : undefined}
            required
          >
            <select id="departmentId" className="select w-full" {...register('departmentId')}>
              <option value="">Select a department</option>
              {departmentOptions.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
          </FormField>
          <FormField
            label="Designation"
            htmlFor="designationId"
            error={errors.designationId?.message}
            hint={designationOptions.length === 0 ? 'No active designations. Create one first.' : undefined}
            required
          >
            <select id="designationId" className="select w-full" {...register('designationId')}>
              <option value="">Select a designation</option>
              {designationOptions.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
          </FormField>
          <FormField label="Joining date" htmlFor="joiningDate" error={errors.joiningDate?.message} required>
            <input id="joiningDate" type="date" className="input w-full" {...register('joiningDate')} />
          </FormField>
          <FormField label="Employment type" htmlFor="employmentType" error={errors.employmentType?.message}>
            <select id="employmentType" className="select w-full" {...register('employmentType')}>
              {EMPLOYMENT_TYPES.map((type) => (
                <option key={type} value={type}>
                  {enumLabel(type)}
                </option>
              ))}
            </select>
          </FormField>
          <FormField
            label="Basic salary (monthly)"
            htmlFor="basicSalary"
            error={errors.basicSalary?.message}
            hint={mode === 'edit' ? 'Leave blank to keep the current salary.' : 'Optional.'}
          >
            <input id="basicSalary" inputMode="decimal" className="input w-full" placeholder="0.00" {...register('basicSalary')} />
          </FormField>
          {mode === 'edit' && (
            <FormField
              label="Status"
              htmlFor="status"
              error={errors.status?.message}
              hint="Returning a former employee to Active or On leave needs an active department and designation."
            >
              <select id="status" className="select w-full" {...register('status')}>
                {EMPLOYEE_STATUSES.map((status) => (
                  <option key={status} value={status}>
                    {enumLabel(status)}
                  </option>
                ))}
              </select>
            </FormField>
          )}
        </Section>
      </div>

      <div className="flex flex-col gap-6">
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body">
            <h2 className="font-semibold">Photo</h2>
            <EmployeePhotoField
              firstName={firstName}
              lastName={lastName}
              currentPhotoUrl={employee?.photoUrl ?? null}
              file={photo}
              onFileChange={setPhoto}
              onRemoveCurrent={onRemovePhoto}
            />
          </div>
        </div>

        <div className="card bg-base-100 shadow-sm lg:sticky lg:top-20">
          <div className="card-body gap-3">
            <button type="submit" className="btn btn-primary w-full" disabled={isSubmitting}>
              {isSubmitting ? <span className="loading loading-spinner loading-sm" /> : <Save className="size-4" />}
              {mode === 'create' ? 'Create employee' : 'Save changes'}
            </button>
            <Link to={cancelTo} className="btn btn-ghost w-full">
              Cancel
            </Link>
          </div>
        </div>
      </div>
    </form>
  )
}
