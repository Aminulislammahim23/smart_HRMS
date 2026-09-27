import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { ADDRESS_TYPES, type Address, type AddressType, type SaveAddressRequest } from '../../types/profile'
import { blankToNull } from '../../utils/formatters'
import { optionalText, requiredText } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'
import type { RecordFormProps } from './ProfileRecordSection'

// CreateEmployeeAddressDto
const schema = z.object({
  addressType: z.enum(ADDRESS_TYPES, { message: 'Select an address type.' }),
  address: requiredText('Address', 500),
  city: requiredText('City', 100),
  district: requiredText('District', 100),
  postalCode: optionalText('Postal code', 20).refine(
    (value) => value === '' || /^[A-Za-z0-9][A-Za-z0-9 -]{1,18}[A-Za-z0-9]$/.test(value),
    'The postal code must be 3 to 20 letters, digits, spaces or dashes.',
  ),
})

type FormValues = z.infer<typeof schema>

interface AddressFormProps extends RecordFormProps<Address, SaveAddressRequest> {
  /** Types the employee already has; each type is allowed once. */
  usedTypes: AddressType[]
}

export function AddressForm({ initial, usedTypes, onSubmit, onCancel }: AddressFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null)
  const availableTypes = ADDRESS_TYPES.filter((type) => type === initial?.addressType || !usedTypes.includes(type))
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      addressType: initial?.addressType ?? availableTypes[0],
      address: initial?.address ?? '',
      city: initial?.city ?? '',
      district: initial?.district ?? '',
      postalCode: initial?.postalCode ?? '',
    },
  })

  const submit = handleSubmit(async (values) => {
    setSubmitError(null)
    try {
      await onSubmit({ ...values, postalCode: blankToNull(values.postalCode) })
    } catch (error) {
      setSubmitError(error)
    }
  })

  return (
    <form onSubmit={submit} noValidate className="grid gap-4 sm:grid-cols-2">
      <div className="sm:col-span-2">
        <ApiErrorAlert error={submitError} />
      </div>
      <FormField label="Address type" htmlFor="addressType" error={errors.addressType?.message} hint="An employee can have one of each." required>
        <select id="addressType" className="select w-full" {...register('addressType')}>
          {availableTypes.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </select>
      </FormField>
      <FormField label="Postal code" htmlFor="postalCode" error={errors.postalCode?.message}>
        <input id="postalCode" className="input w-full" {...register('postalCode')} />
      </FormField>
      <FormField label="Address" htmlFor="address" error={errors.address?.message} className="sm:col-span-2" required>
        <textarea id="address" rows={2} className="textarea w-full" placeholder="House, road, area" {...register('address')} />
      </FormField>
      <FormField label="City" htmlFor="city" error={errors.city?.message} required>
        <input id="city" className="input w-full" {...register('city')} />
      </FormField>
      <FormField label="District" htmlFor="district" error={errors.district?.message} required>
        <input id="district" className="input w-full" {...register('district')} />
      </FormField>
      <div className="sm:col-span-2">
        <FormActions submitting={isSubmitting} onCancel={onCancel} />
      </div>
    </form>
  )
}
