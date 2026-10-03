import { zodResolver } from '@hookform/resolvers/zod'
import { Ban, Check, X } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useToast } from '../../hooks/useToast'
import { applyForLeave, approveLeave, cancelLeave, rejectLeave } from '../../services/leaveService'
import type { Employee } from '../../types/employee'
import { LEAVE_TYPES, type LeaveRequest, type LeaveStatus } from '../../types/leave'
import { blankToNull, enumLabel, formatDate, formatDateTime, todayInput } from '../../utils/formatters'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormField } from '../common/FormField'
import { Modal } from '../common/Modal'
import { Table, type Column } from '../common/Table'

const STATUS_CLASSES: Record<LeaveStatus, string> = {
  Pending: 'badge-soft badge-warning',
  Approved: 'badge-soft badge-success',
  Rejected: 'badge-soft badge-error',
  Cancelled: 'badge-outline',
}

export function LeaveStatusBadge({ status }: { status: LeaveStatus }) {
  return <span className={`badge badge-sm whitespace-nowrap ${STATUS_CLASSES[status]}`}>{status}</span>
}

export type LeaveDecision = 'approve' | 'reject' | 'cancel'

interface LeaveTableProps {
  requests: readonly LeaveRequest[]
  showEmployee?: boolean
  onDecide: (request: LeaveRequest, decision: LeaveDecision) => void
}

/** Leave requests. Approve/reject/cancel buttons appear only where the server says the user may use them. */
export function LeaveTable({ requests, showEmployee = false, onDecide }: LeaveTableProps) {
  const columns: Column<LeaveRequest>[] = [
    ...(showEmployee
      ? [
          {
            key: 'employee',
            header: 'Employee',
            render: (r: LeaveRequest) => (
              <div className="min-w-0">
                <p className="truncate font-medium">{r.employeeName}</p>
                <p className="text-xs text-base-content/60">{r.employeeCode}</p>
              </div>
            ),
          },
        ]
      : []),
    {
      key: 'type',
      header: 'Type',
      render: (r) => (
        <span className="whitespace-nowrap">
          {r.leaveType} {!r.isPaid && <span className="badge badge-xs badge-soft badge-error ml-1">Unpaid</span>}
        </span>
      ),
    },
    {
      key: 'dates',
      header: 'Dates',
      render: (r) => (
        <span className="whitespace-nowrap">
          {formatDate(r.startDate)}
          {r.endDate !== r.startDate && ` – ${formatDate(r.endDate)}`}
        </span>
      ),
    },
    { key: 'days', header: 'Days', render: (r) => r.totalDays, className: 'text-right' },
    { key: 'reason', header: 'Reason', render: (r) => <span className="line-clamp-2 max-w-xs">{r.reason ?? '—'}</span>, className: 'hidden lg:table-cell' },
    { key: 'status', header: 'Status', render: (r) => <LeaveStatusBadge status={r.status} /> },
    {
      key: 'review',
      header: 'Reviewed',
      className: 'hidden md:table-cell',
      render: (r) =>
        r.reviewedBy ? (
          <div className="text-xs">
            <p>{r.reviewedBy}</p>
            <p className="text-base-content/60">{formatDateTime(r.reviewedAt)}</p>
            {r.reviewComment && <p className="italic text-base-content/70">“{r.reviewComment}”</p>}
          </div>
        ) : (
          '—'
        ),
    },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (r) => (
        <div className="flex justify-end gap-1">
          {r.canReview && (
            <>
              <button type="button" className="btn btn-xs btn-success btn-soft" onClick={() => onDecide(r, 'approve')} aria-label={`Approve leave of ${r.employeeName}`}>
                <Check className="size-3.5" /> Approve
              </button>
              <button type="button" className="btn btn-xs btn-error btn-soft" onClick={() => onDecide(r, 'reject')} aria-label={`Reject leave of ${r.employeeName}`}>
                <X className="size-3.5" /> Reject
              </button>
            </>
          )}
          {r.canCancel && (
            <button type="button" className="btn btn-xs btn-ghost text-error" onClick={() => onDecide(r, 'cancel')} aria-label="Cancel leave request">
              <Ban className="size-3.5" /> Cancel
            </button>
          )}
        </div>
      ),
    },
  ]

  return <Table columns={columns} rows={requests} rowKey={(r) => r.id} compact />
}

const DECISION_TEXT: Record<LeaveDecision, { title: string; label: string; button: string; done: string }> = {
  approve: { title: 'Approve leave', label: 'Approve', button: 'btn-success', done: 'approved' },
  reject: { title: 'Reject leave', label: 'Reject', button: 'btn-error', done: 'rejected' },
  cancel: { title: 'Cancel leave request', label: 'Cancel request', button: 'btn-error', done: 'cancelled' },
}

/** Approve, reject or cancel with an optional comment. The server decides whether the user may. */
export function LeaveDecisionModal({ request, decision, onClose, onDone }: { request: LeaveRequest; decision: LeaveDecision; onClose: () => void; onDone: () => void }) {
  const { notify } = useToast()
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const text = DECISION_TEXT[decision]

  const submit = async () => {
    setBusy(true)
    setError(null)
    try {
      const action = decision === 'approve' ? approveLeave : decision === 'reject' ? rejectLeave : cancelLeave
      await action(request.id, blankToNull(comment))
      notify('success', `Leave request ${text.done}.`)
      onDone()
      onClose()
    } catch (caught) {
      setError(caught)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open title={text.title} onClose={onClose} busy={busy}>
      <div className="flex flex-col gap-3">
        <p className="text-sm">
          <span className="font-medium">{request.employeeName}</span> · {request.leaveType} leave · {formatDate(request.startDate)}
          {request.endDate !== request.startDate && ` – ${formatDate(request.endDate)}`} ({request.totalDays} working {request.totalDays === 1 ? 'day' : 'days'})
          {!request.isPaid && <span className="text-error"> · unpaid: deducted from salary</span>}
        </p>
        {request.reason && <p className="rounded-box bg-base-200 p-3 text-sm">“{request.reason}”</p>}
        {error !== null && <ApiErrorAlert error={error} />}
        <FormField label="Comment" htmlFor="leave-comment" hint="Optional; visible to the employee.">
          <textarea id="leave-comment" rows={2} maxLength={500} className="textarea w-full" value={comment} onChange={(e) => setComment(e.target.value)} />
        </FormField>
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>
            Close
          </button>
          <button type="button" className={`btn ${text.button}`} onClick={submit} disabled={busy}>
            {busy && <span className="loading loading-spinner loading-sm" />} {text.label}
          </button>
        </div>
      </div>
    </Modal>
  )
}

const applySchema = z
  .object({
    employeeId: z.string(),
    leaveType: z.enum(LEAVE_TYPES, { message: 'Choose a leave type.' }),
    startDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Choose a start date.'),
    endDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Choose an end date.'),
    reason: z.string().max(500, 'At most 500 characters.'),
  })
  .refine((v) => v.startDate <= v.endDate, { message: 'The end date cannot be before the start date.', path: ['endDate'] })
type ApplyForm = z.infer<typeof applySchema>

/**
 * Apply for leave. Weekends are not counted; the server calculates the working days and checks for overlaps.
 * HR/Admin can pick another employee (the server ignores that choice for everyone else).
 */
export function ApplyLeaveModal({ employees, onClose, onDone }: { employees?: Employee[]; onClose: () => void; onDone: () => void }) {
  const { notify } = useToast()
  const [error, setError] = useState<unknown>(null)
  const today = todayInput()
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ApplyForm>({ resolver: zodResolver(applySchema), defaultValues: { employeeId: '', leaveType: 'Annual', startDate: today, endDate: today, reason: '' } })

  const submit = handleSubmit(async (values) => {
    setError(null)
    try {
      const created = await applyForLeave({
        employeeId: values.employeeId || undefined,
        leaveType: values.leaveType,
        startDate: values.startDate,
        endDate: values.endDate,
        reason: blankToNull(values.reason),
      })
      notify('success', `Leave request for ${created.totalDays} working ${created.totalDays === 1 ? 'day' : 'days'} submitted.`)
      onDone()
      onClose()
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <Modal open title="Apply for leave" onClose={onClose} busy={isSubmitting}>
      <form className="flex flex-col gap-3" onSubmit={submit} noValidate>
        {error !== null && <ApiErrorAlert error={error} />}
        {employees && (
          <FormField label="Employee" htmlFor="leave-employee" hint="Leave empty to apply for yourself.">
            <select id="leave-employee" className="select w-full" {...register('employeeId')}>
              <option value="">Myself</option>
              {employees.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName} ({e.employeeCode})
                </option>
              ))}
            </select>
          </FormField>
        )}
        <FormField label="Leave type" htmlFor="leave-type" error={errors.leaveType?.message} hint="Unpaid leave is deducted from salary." required>
          <select id="leave-type" className="select w-full" {...register('leaveType')}>
            {LEAVE_TYPES.map((type) => (
              <option key={type} value={type}>
                {enumLabel(type)}
              </option>
            ))}
          </select>
        </FormField>
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="From" htmlFor="leave-start" error={errors.startDate?.message} required>
            <input id="leave-start" type="date" className="input w-full" {...register('startDate')} />
          </FormField>
          <FormField label="To" htmlFor="leave-end" error={errors.endDate?.message} required>
            <input id="leave-end" type="date" className="input w-full" {...register('endDate')} />
          </FormField>
        </div>
        <FormField label="Reason" htmlFor="leave-reason" error={errors.reason?.message}>
          <textarea id="leave-reason" rows={2} className="textarea w-full" {...register('reason')} />
        </FormField>
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting && <span className="loading loading-spinner loading-sm" />} Submit request
          </button>
        </div>
      </form>
    </Modal>
  )
}
