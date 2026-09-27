import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import type { Education, SaveEducationRequest } from '../../types/profile'
import { blankToNull } from '../../utils/formatters'
import { optionalText, requiredText } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'
import type { RecordFormProps } from './ProfileRecordSection'

interface EducationFormProps extends RecordFormProps<Education, SaveEducationRequest> {
  /** The backend requires the passing year to be no earlier than the employee's birth year. */
  birthYear: number
}

export function EducationForm({ initial, birthYear, onSubmit, onCancel }: EducationFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null)
  const maxYear = new Date().getFullYear() + 5

  // CreateEmployeeEducationDto + the passing-year rule in EmployeeEducationService.
  const schema = z.object({
    degree: requiredText('Degree', 100),
    institution: requiredText('Institution', 200),
    major: optionalText('Major', 150),
    result: optionalText('Result', 50),
    passingYear: z
      .string()
      .trim()
      .min(1, 'Passing year is required.')
      .regex(/^\d{4}$/, 'Enter a 4-digit year.')
      .refine((value) => Number(value) >= birthYear && Number(value) <= maxYear, `The passing year must be between ${birthYear} and ${maxYear}.`),
  })
  type FormValues = z.infer<typeof schema>

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      degree: initial?.degree ?? '',
      institution: initial?.institution ?? '',
      major: initial?.major ?? '',
      result: initial?.result ?? '',
      passingYear: initial?.passingYear.toString() ?? '',
    },
  })

  const submit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await onSubmit({
        degree: values.degree,
        institution: values.institution,
        major: blankToNull(values.major),
        result: blankToNull(values.result),
        passingYear: Number(values.passingYear),
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
      <FormField label="Degree" htmlFor="degree" error={errors.degree?.message} required>
        <input id="degree" className="input w-full" placeholder="SSC, HSC, BSc, MBA…" {...register('degree')} />
      </FormField>
      <FormField label="Institution" htmlFor="institution" error={errors.institution?.message} required>
        <input id="institution" className="input w-full" {...register('institution')} />
      </FormField>
      <FormField label="Major / subject" htmlFor="major" error={errors.major?.message}>
        <input id="major" className="input w-full" {...register('major')} />
      </FormField>
      <FormField label="Result" htmlFor="result" error={errors.result?.message} hint='E.g. "GPA 5.00", "CGPA 3.75".'>
        <input id="result" className="input w-full" {...register('result')} />
      </FormField>
      <FormField label="Passing year" htmlFor="passingYear" error={errors.passingYear?.message} required>
        <input id="passingYear" inputMode="numeric" className="input w-full" placeholder={String(new Date().getFullYear())} {...register('passingYear')} />
      </FormField>
      <div className="sm:col-span-2">
        <FormActions submitting={isSubmitting} onCancel={onCancel} />
      </div>
    </form>
  )
}
