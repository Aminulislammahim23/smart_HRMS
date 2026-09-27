import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import type { Experience, SaveExperienceRequest } from '../../types/profile'
import { blankToNull, toDateInput, todayInput } from '../../utils/formatters'
import { optionalText, requiredDate, requiredText } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'
import type { RecordFormProps } from './ProfileRecordSection'

interface ExperienceFormProps extends RecordFormProps<Experience, SaveExperienceRequest> {
  /** yyyy-MM-dd. The backend requires the start date to be after the date of birth. */
  dateOfBirth: string
}

export function ExperienceForm({ initial, dateOfBirth, onSubmit, onCancel }: ExperienceFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null)
  const today = todayInput()

  // CreateEmployeeExperienceDto + the date rules in EmployeeExperienceService.
  const schema = z
    .object({
      companyName: requiredText('Company name', 200),
      designation: requiredText('Designation', 150),
      startDate: requiredDate('Start date')
        .refine((value) => value > dateOfBirth, 'The start date must be after the date of birth.')
        .refine((value) => value <= today, 'The start date cannot be in the future.'),
      endDate: z.string().refine((value) => value === '' || value <= today, 'The end date cannot be in the future.'),
      responsibilities: optionalText('Responsibilities', 2000),
    })
    .refine((values) => !values.endDate || !values.startDate || values.endDate >= values.startDate, {
      path: ['endDate'],
      message: 'The end date cannot be before the start date.',
    })
  type FormValues = z.infer<typeof schema>

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      companyName: initial?.companyName ?? '',
      designation: initial?.designation ?? '',
      startDate: toDateInput(initial?.startDate),
      endDate: toDateInput(initial?.endDate),
      responsibilities: initial?.responsibilities ?? '',
    },
  })

  const submit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await onSubmit({
        companyName: values.companyName,
        designation: values.designation,
        startDate: values.startDate,
        endDate: blankToNull(values.endDate),
        responsibilities: blankToNull(values.responsibilities),
      })
    } catch (error) {
      setSubmitError(error)
    }
  })

  return (
    <form onSubmit={submit} noValidate className="grid gap-4 sm:grid-cols-2">
      <div className="sm:col-span-2">
        <ApiErrorAlert error={submitError} />
      </div>
      <FormField label="Company" htmlFor="companyName" error={errors.companyName?.message} required>
        <input id="companyName" className="input w-full" {...register('companyName')} />
      </FormField>
      <FormField label="Job title" htmlFor="designation" error={errors.designation?.message} required>
        <input id="designation" className="input w-full" {...register('designation')} />
      </FormField>
      <FormField label="Start date" htmlFor="startDate" error={errors.startDate?.message} required>
        <input id="startDate" type="date" className="input w-full" max={today} {...register('startDate')} />
      </FormField>
      <FormField label="End date" htmlFor="endDate" error={errors.endDate?.message} hint="Leave empty if this is a current job.">
        <input id="endDate" type="date" className="input w-full" max={today} {...register('endDate')} />
      </FormField>
      <FormField label="Responsibilities" htmlFor="responsibilities" error={errors.responsibilities?.message} className="sm:col-span-2">
        <textarea id="responsibilities" rows={4} className="textarea w-full" {...register('responsibilities')} />
      </FormField>
      <div className="sm:col-span-2">
        <FormActions submitting={isSubmitting} onCancel={onCancel} />
      </div>
    </form>
  )
}
