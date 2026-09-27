import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import {
  BLOOD_GROUPS,
  GENDERS,
  MARITAL_STATUSES,
  type PersonalDetails,
  type SavePersonalDetailsRequest,
} from '../../types/profile'
import { blankToNull, enumLabel } from '../../utils/formatters'
import { optionalEnum, optionalText } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'

// CreateEmployeePersonalDetailsDto
const schema = z.object({
  gender: optionalEnum(GENDERS),
  maritalStatus: optionalEnum(MARITAL_STATUSES),
  bloodGroup: optionalEnum(BLOOD_GROUPS),
  nationality: optionalText('Nationality', 100),
  nationalId: z
    .string()
    .trim()
    .refine((value) => value === '' || /^(\d{10}|\d{13}|\d{17})$/.test(value), 'The National ID must be 10, 13 or 17 digits.'),
  passportNo: z
    .string()
    .trim()
    .refine((value) => value === '' || /^[A-Za-z0-9]{6,20}$/.test(value), 'The passport number must be 6 to 20 letters or digits.'),
})

type FormValues = z.infer<typeof schema>

interface PersonalDetailsFormProps {
  /** Full (unmasked) details when editing. */
  initial?: PersonalDetails
  onSubmit: (data: SavePersonalDetailsRequest) => Promise<void>
  onCancel: () => void
}

export function PersonalDetailsForm({ initial, onSubmit, onCancel }: PersonalDetailsFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      gender: initial?.gender ?? '',
      maritalStatus: initial?.maritalStatus ?? '',
      bloodGroup: initial?.bloodGroup ?? '',
      nationality: initial?.nationality ?? '',
      nationalId: initial?.nationalId ?? '',
      passportNo: initial?.passportNo ?? '',
    },
  })

  const submit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await onSubmit({
        gender: values.gender || null,
        maritalStatus: values.maritalStatus || null,
        bloodGroup: values.bloodGroup || null,
        nationality: blankToNull(values.nationality),
        nationalId: blankToNull(values.nationalId),
        passportNo: blankToNull(values.passportNo)?.toUpperCase() ?? null,
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
      <FormField label="Gender" htmlFor="gender" error={errors.gender?.message}>
        <select id="gender" className="select w-full" {...register('gender')}>
          <option value="">Not specified</option>
          {GENDERS.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Marital status" htmlFor="maritalStatus" error={errors.maritalStatus?.message}>
        <select id="maritalStatus" className="select w-full" {...register('maritalStatus')}>
          <option value="">Not specified</option>
          {MARITAL_STATUSES.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Blood group" htmlFor="bloodGroup" error={errors.bloodGroup?.message}>
        <select id="bloodGroup" className="select w-full" {...register('bloodGroup')}>
          <option value="">Not specified</option>
          {BLOOD_GROUPS.map((value) => (
            <option key={value} value={value}>
              {enumLabel(value)}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Nationality" htmlFor="nationality" error={errors.nationality?.message}>
        <input id="nationality" className="input w-full" placeholder="Bangladeshi" {...register('nationality')} />
      </FormField>
      <FormField label="National ID" htmlFor="nationalId" error={errors.nationalId?.message} hint="10, 13 or 17 digits. Must be unique.">
        <input id="nationalId" inputMode="numeric" className="input w-full" autoComplete="off" {...register('nationalId')} />
      </FormField>
      <FormField label="Passport number" htmlFor="passportNo" error={errors.passportNo?.message} hint="6–20 letters or digits. Must be unique.">
        <input id="passportNo" className="input w-full uppercase" autoComplete="off" {...register('passportNo')} />
      </FormField>
      <p className="text-xs text-base-content/60 sm:col-span-2">
        Saving replaces all personal details. The date of birth is part of the employee record and is changed on the edit page.
      </p>
      <div className="sm:col-span-2">
        <FormActions submitting={isSubmitting} onCancel={onCancel} />
      </div>
    </form>
  )
}
