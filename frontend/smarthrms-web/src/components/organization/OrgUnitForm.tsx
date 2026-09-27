import { zodResolver } from '@hookform/resolvers/zod'
import { Save } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import { z } from 'zod'
import { blankToNull } from '../../utils/formatters'
import { optionalText, requiredText } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormField } from '../common/FormField'
import type { OrgUnit, OrgUnitModule, UpdateOrgUnitRequest } from './orgUnits'

// Create/Update(Department|Designation)Dto: Name [Required][MaxLength(100)], Description [MaxLength(500)].
const schema = z.object({
  name: requiredText('Name', 100),
  description: optionalText('Description', 500),
  isActive: z.boolean(),
})

type FormValues = z.infer<typeof schema>

interface OrgUnitFormProps {
  module: OrgUnitModule
  /** Absent when creating. */
  initial?: OrgUnit
  onSubmit: (data: UpdateOrgUnitRequest) => Promise<void>
}

export function OrgUnitForm({ module, initial, onSubmit }: OrgUnitFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      description: initial?.description ?? '',
      isActive: initial?.isActive ?? true,
    },
  })

  const submit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await onSubmit({ name: values.name, description: blankToNull(values.description), isActive: values.isActive })
    } catch (error) {
      setSubmitError(error)
    }
  })

  const noun = module.singular.toLowerCase()

  return (
    <form onSubmit={submit} noValidate className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <ApiErrorAlert error={submitError} />

        <FormField label="Name" htmlFor="name" error={errors.name?.message} required>
          <input id="name" className="input w-full" autoFocus {...register('name')} />
        </FormField>

        <FormField label="Description" htmlFor="description" error={errors.description?.message} hint="Optional. Up to 500 characters.">
          <textarea id="description" rows={4} className="textarea w-full" {...register('description')} />
        </FormField>

        {initial && (
          <label className="label cursor-pointer justify-start gap-3">
            <input type="checkbox" className="toggle toggle-success" {...register('isActive')} />
            <span className="text-sm">
              Active
              <span className="block text-xs text-base-content/60">
                Inactive {module.plural.toLowerCase()} can't be assigned to employees. A {noun} with active or on-leave
                employees can't be deactivated.
              </span>
            </span>
          </label>
        )}

        <div className="card-actions justify-end">
          <Link to={module.basePath} className="btn btn-ghost">
            Cancel
          </Link>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting ? <span className="loading loading-spinner loading-sm" /> : <Save className="size-4" />}
            {initial ? 'Save changes' : `Create ${noun}`}
          </button>
        </div>
      </div>
    </form>
  )
}
