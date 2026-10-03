import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import type { PayrollPeriod, PayrollPeriodRequest } from '../../types/payroll'
import { blankToNull, formatMonth, monthRange, todayInput } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormField } from '../common/FormField'

const DATE = /^\d{4}-\d{2}-\d{2}$/
const DAY_MS = 86_400_000

const schema = z
  .object({
    month: z.string(),
    name: z.string().max(100, 'At most 100 characters.'),
    startDate: z.string().regex(DATE, 'Choose a start date.'),
    endDate: z.string().regex(DATE, 'Choose an end date.'),
    notes: z.string().max(500, 'At most 500 characters.'),
  })
  .refine((v) => v.startDate < v.endDate, { message: 'The end date must be after the start date.', path: ['endDate'] })
  .refine((v) => (Date.parse(v.endDate) - Date.parse(v.startDate)) / DAY_MS + 1 <= 31, { message: 'A payroll period can cover at most 31 days.', path: ['endDate'] })
type Form = z.infer<typeof schema>

interface PayrollPeriodFormProps {
  period?: PayrollPeriod
  submitLabel: string
  onSubmit: (request: PayrollPeriodRequest) => Promise<void>
  onCancel: () => void
}

/**
 * Create or edit a payroll period. Picking a month fills in its first and last day; custom dates are allowed too.
 * Overlaps with other periods are checked by the server.
 */
export function PayrollPeriodForm({ period, submitLabel, onSubmit, onCancel }: PayrollPeriodFormProps) {
  const [error, setError] = useState<unknown>(null)
  const initialMonth = period?.startDate.slice(0, 7) ?? todayInput().slice(0, 7)
  const initialRange = monthRange(initialMonth)
  const {
    register,
    handleSubmit,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = useForm<Form>({
    resolver: zodResolver(schema),
    defaultValues: {
      month: initialMonth,
      name: period?.name ?? '',
      startDate: period?.startDate ?? initialRange.start,
      endDate: period?.endDate ?? initialRange.end,
      notes: period?.notes ?? '',
    },
  })
  const month = useWatch({ control, name: 'month' })

  const pickMonth = (value: string) => {
    setValue('month', value)
    if (!value) return
    const range = monthRange(value)
    setValue('startDate', range.start, { shouldValidate: true })
    setValue('endDate', range.end, { shouldValidate: true })
  }

  const submit = handleSubmit(async (values) => {
    setError(null)
    try {
      await onSubmit({ name: blankToNull(values.name), startDate: values.startDate, endDate: values.endDate, notes: blankToNull(values.notes) })
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      {error !== null && <ApiErrorAlert error={error} />}
      <div className="grid gap-4 md:grid-cols-3">
        <FormField label="Month" htmlFor="pp-month" hint={month ? `Fills in ${formatMonth(month)}` : 'Or enter custom dates'}>
          <input id="pp-month" type="month" className="input w-full" value={month} onChange={(event) => pickMonth(event.target.value)} />
        </FormField>
        <FormField label="Start date" htmlFor="pp-start" error={errors.startDate?.message} required>
          <input id="pp-start" type="date" className="input w-full" {...register('startDate', { onChange: () => setValue('month', '') })} />
        </FormField>
        <FormField label="End date" htmlFor="pp-end" error={errors.endDate?.message} required>
          <input id="pp-end" type="date" className="input w-full" {...register('endDate', { onChange: () => setValue('month', '') })} />
        </FormField>
      </div>
      <FormField label="Name" htmlFor="pp-name" error={errors.name?.message} hint="Optional. Defaults to the month name, e.g. “October 2026”.">
        <input id="pp-name" className="input w-full" {...register('name')} />
      </FormField>
      <FormField label="Notes" htmlFor="pp-notes" error={errors.notes?.message}>
        <textarea id="pp-notes" rows={2} className="textarea w-full" {...register('notes')} />
      </FormField>
      <div className="flex justify-end gap-2">
        <button type="button" className="btn btn-ghost" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </button>
        <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
          {isSubmitting && <span className="loading loading-spinner loading-sm" />} {submitLabel}
        </button>
      </div>
    </form>
  )
}
