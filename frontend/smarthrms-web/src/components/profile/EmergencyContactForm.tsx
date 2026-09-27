import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import type { EmergencyContact, SaveEmergencyContactRequest } from '../../types/profile'
import { blankToNull } from '../../utils/formatters'
import { optionalEmail, optionalText, requiredPhone, requiredText } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'
import type { RecordFormProps } from './ProfileRecordSection'

// CreateEmployeeEmergencyContactDto
const schema = z.object({
  name: requiredText('Name', 150),
  relationship: requiredText('Relationship', 50),
  phone: requiredPhone(30),
  email: optionalEmail(200),
  address: optionalText('Address', 500),
})

type FormValues = z.infer<typeof schema>

export function EmergencyContactForm({ initial, onSubmit, onCancel }: RecordFormProps<EmergencyContact, SaveEmergencyContactRequest>) {
  const [submitError, setSubmitError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      relationship: initial?.relationship ?? '',
      phone: initial?.phone ?? '',
      email: initial?.email ?? '',
      address: initial?.address ?? '',
    },
  })

  const submit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await onSubmit({ ...values, email: blankToNull(values.email), address: blankToNull(values.address) })
    } catch (error) {
      setSubmitError(error)
    }
  })

  return (
    <form onSubmit={submit} noValidate className="grid gap-4 sm:grid-cols-2">
      <div className="sm:col-span-2">
        <ApiErrorAlert error={submitError} />
      </div>
      <FormField label="Name" htmlFor="name" error={errors.name?.message} required>
        <input id="name" className="input w-full" {...register('name')} />
      </FormField>
      <FormField label="Relationship" htmlFor="relationship" error={errors.relationship?.message} required>
        <input id="relationship" className="input w-full" placeholder="Spouse, Father, Sibling…" list="relationship-options" {...register('relationship')} />
        <datalist id="relationship-options">
          {['Spouse', 'Father', 'Mother', 'Sibling', 'Son', 'Daughter', 'Friend'].map((option) => (
            <option key={option} value={option} />
          ))}
        </datalist>
      </FormField>
      <FormField label="Phone" htmlFor="phone" error={errors.phone?.message} hint="Each contact needs a different phone number." required>
        <input id="phone" type="tel" className="input w-full" {...register('phone')} />
      </FormField>
      <FormField label="Email" htmlFor="email" error={errors.email?.message}>
        <input id="email" type="email" className="input w-full" {...register('email')} />
      </FormField>
      <FormField label="Address" htmlFor="address" error={errors.address?.message} className="sm:col-span-2">
        <textarea id="address" rows={2} className="textarea w-full" {...register('address')} />
      </FormField>
      <div className="sm:col-span-2">
        <FormActions submitting={isSubmitting} onCancel={onCancel} />
      </div>
    </form>
  )
}
