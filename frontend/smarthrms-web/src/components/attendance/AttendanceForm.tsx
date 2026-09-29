import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { ATTENDANCE_STATUSES, type Attendance, type CreateAttendanceRequest } from '../../types/attendance'
import type { Employee } from '../../types/employee'
import { blankToNull, enumLabel, todayInput } from '../../utils/formatters'
import { optionalText } from '../../utils/validators'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormActions } from '../common/FormActions'
import { FormField } from '../common/FormField'

// Mirrors CreateAttendanceDto / UpdateAttendanceDto and the rules in AttendanceService (the server re-checks them).
const schema = z
  .object({
    employeeId: z.string(),
    attendanceDate: z.string().min(1, 'Date is required.'),
    status: z.enum(ATTENDANCE_STATUSES, { message: 'Select a status.' }),
    checkInTime: z.string(),
    checkOutTime: z.string(),
    remarks: optionalText('Remarks', 500),
  })
  .superRefine((v, ctx) => {
    const noTimes = v.status === 'Absent' || v.status === 'Leave'
    if (noTimes && (v.checkInTime || v.checkOutTime)) {
      ctx.addIssue({ code: 'custom', path: ['checkInTime'], message: `A ${enumLabel(v.status).toLowerCase()} record can't have check-in or check-out times.` })
    }
    if (v.checkOutTime && !v.checkInTime) {
      ctx.addIssue({ code: 'custom', path: ['checkOutTime'], message: 'A check-out time requires a check-in time.' })
    }
    if (v.checkOutTime && v.checkInTime && v.checkOutTime < v.checkInTime) {
      ctx.addIssue({ code: 'custom', path: ['checkOutTime'], message: 'The check-out time cannot be earlier than the check-in time.' })
    }
  })

type FormValues = z.infer<typeof schema>

interface AttendanceFormProps {
  /** Absent when creating a record. */
  initial?: Attendance
  /** Needed when creating: current employees only (the server rejects others). */
  employees?: Employee[]
  defaultEmployeeId?: string
  defaultDate?: string
  onSubmit: (data: CreateAttendanceRequest) => Promise<void>
  onCancel: () => void
}

export function AttendanceForm({ initial, employees = [], defaultEmployeeId = '', defaultDate, onSubmit, onCancel }: AttendanceFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null)
  const creating = !initial
  const {
    register,
    handleSubmit,
    control,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      employeeId: initial?.employeeId ?? defaultEmployeeId,
      attendanceDate: initial?.attendanceDate ?? defaultDate ?? todayInput(),
      status: initial?.status ?? 'Present',
      checkInTime: initial?.checkInTime?.slice(0, 5) ?? '',
      checkOutTime: initial?.checkOutTime?.slice(0, 5) ?? '',
      remarks: initial?.remarks ?? '',
    },
  })
  const status = useWatch({ control, name: 'status' })
  const timesDisabled = status === 'Absent' || status === 'Leave'

  const submit = handleSubmit(async (values) => {
    if (creating && !values.employeeId) {
      setError('employeeId', { message: 'Select an employee.' })
      return
    }
    setSubmitError(null)
    try {
      await onSubmit({
        employeeId: values.employeeId,
        attendanceDate: values.attendanceDate,
        status: values.status,
        checkInTime: timesDisabled ? null : blankToNull(values.checkInTime),
        checkOutTime: timesDisabled ? null : blankToNull(values.checkOutTime),
        remarks: blankToNull(values.remarks),
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
      {creating && (
        <>
          <FormField label="Employee" htmlFor="employeeId" error={errors.employeeId?.message} required>
            <select id="employeeId" className="select w-full" {...register('employeeId')}>
              <option value="">Select an employee</option>
              {employees.map((employee) => (
                <option key={employee.id} value={employee.id}>
                  {employee.fullName} ({employee.employeeCode})
                </option>
              ))}
            </select>
          </FormField>
          <FormField label="Date" htmlFor="attendanceDate" error={errors.attendanceDate?.message} hint="Future dates are allowed only for leave." required>
            <input id="attendanceDate" type="date" className="input w-full" {...register('attendanceDate')} />
          </FormField>
        </>
      )}
      <FormField label="Status" htmlFor="status" error={errors.status?.message} required>
        <select id="status" className="select w-full" {...register('status')}>
          {ATTENDANCE_STATUSES.map((value) => (
            <option key={value} value={value}>
              {enumLabel(value)}
            </option>
          ))}
        </select>
      </FormField>
      <div className="hidden sm:block" />
      <FormField label="Check-in time" htmlFor="checkInTime" error={errors.checkInTime?.message} hint={timesDisabled ? 'Not used for this status.' : 'Office time.'}>
        <input id="checkInTime" type="time" className="input w-full" disabled={timesDisabled} {...register('checkInTime')} />
      </FormField>
      <FormField label="Check-out time" htmlFor="checkOutTime" error={errors.checkOutTime?.message} hint="Working hours are calculated automatically.">
        <input id="checkOutTime" type="time" className="input w-full" disabled={timesDisabled} {...register('checkOutTime')} />
      </FormField>
      <FormField label="Remarks" htmlFor="remarks" error={errors.remarks?.message} className="sm:col-span-2">
        <textarea id="remarks" rows={2} className="textarea w-full" {...register('remarks')} />
      </FormField>
      <div className="sm:col-span-2">
        <FormActions submitting={isSubmitting} onCancel={onCancel} submitLabel={creating ? 'Create record' : 'Save'} />
      </div>
    </form>
  )
}
