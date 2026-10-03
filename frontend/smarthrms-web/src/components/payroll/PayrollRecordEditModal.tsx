import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useToast } from '../../hooks/useToast'
import { updatePayrollRecord } from '../../services/payrollService'
import type { PayrollRecord } from '../../types/payroll'
import { blankToNull, formatAmount } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormField } from '../common/FormField'
import { Modal } from '../common/Modal'
import { amountSchema } from './amountSchema'

const amount = amountSchema

const schema = z.object({
  overtimeAmount: amount,
  bonus: amount,
  advanceDeduction: amount,
  loanDeduction: amount,
  otherDeduction: amount,
  remarks: z.string().max(500, 'At most 500 characters.'),
})
type FormInput = z.input<typeof schema>
type FormOutput = z.output<typeof schema>

const FIELDS: { name: keyof Omit<FormOutput, 'remarks'>; label: string }[] = [
  { name: 'overtimeAmount', label: 'Overtime' },
  { name: 'bonus', label: 'Bonus' },
  { name: 'advanceDeduction', label: 'Advance deduction' },
  { name: 'loanDeduction', label: 'Loan deduction' },
  { name: 'otherDeduction', label: 'Other deduction' },
]

/**
 * Edits the manually entered amounts of a record. Basic, allowances, tax and leave deduction come from the salary
 * structure and attendance; the server recalculates gross, deduction and net.
 */
export function PayrollRecordEditModal({ record, onClose, onSaved }: { record: PayrollRecord; onClose: () => void; onSaved: () => void }) {
  const { notify } = useToast()
  const [error, setError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormOutput>({
    resolver: zodResolver(schema),
    defaultValues: {
      overtimeAmount: record.overtimeAmount,
      bonus: record.bonus,
      advanceDeduction: record.advanceDeduction,
      loanDeduction: record.loanDeduction,
      otherDeduction: record.otherDeduction,
      remarks: record.remarks ?? '',
    },
  })

  const submit = handleSubmit(async (values) => {
    setError(null)
    try {
      const updated = await updatePayrollRecord(record.id, { ...values, remarks: blankToNull(values.remarks) })
      notify(updated.status === 'NeedsReview' ? 'error' : 'success',
        updated.status === 'NeedsReview'
          ? `Saved, but the net salary of ${updated.employeeName} is negative (${formatAmount(updated.netSalary)}).`
          : `Payroll of ${updated.employeeName} updated. Net salary: ${formatAmount(updated.netSalary)}.`)
      onSaved()
      onClose()
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <Modal open title={`Edit payroll — ${record.employeeName}`} onClose={onClose} busy={isSubmitting}>
      <form className="flex flex-col gap-3" onSubmit={submit} noValidate>
        <p className="text-sm text-base-content/70">
          Basic salary, allowances, tax and unpaid-leave deduction are calculated from the salary structure and attendance. To change them, update the
          salary structure and recalculate.
        </p>
        {error !== null && <ApiErrorAlert error={error} />}
        <div className="grid gap-3 sm:grid-cols-2">
          {FIELDS.map(({ name, label }) => (
            <FormField key={name} label={label} htmlFor={`pr-${name}`} error={errors[name]?.message}>
              <input id={`pr-${name}`} type="number" min="0" step="0.01" inputMode="decimal" className="input w-full" {...register(name)} />
            </FormField>
          ))}
        </div>
        <FormField label="Remarks" htmlFor="pr-remarks" error={errors.remarks?.message}>
          <textarea id="pr-remarks" rows={2} className="textarea w-full" {...register('remarks')} />
        </FormField>
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting && <span className="loading loading-spinner loading-sm" />} Save
          </button>
        </div>
      </form>
    </Modal>
  )
}
